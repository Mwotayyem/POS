using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Inventory.Enums;
using SmartApp.Domain.Purchasing;
using SmartApp.Domain.Purchasing.Enums;
using SmartApp.Domain.Sequences;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.PurchaseReturns.Commands.CreatePurchaseReturn;

/// <summary>
/// Posts a purchase return atomically. For each line it checks the requested quantity does not exceed
/// the still-returnable quantity (Quantity − ReturnedQty), removes that stock through
/// <see cref="IStockLedger"/> (outbound), freezes the original unit price, bumps ReturnedQty, and
/// reduces the supplier's payable balance. Finally it recomputes the invoice status
/// (Confirmed → PartiallyReturned → FullyReturned). One SaveChanges commits everything.
/// </summary>
public sealed class CreatePurchaseReturnCommandHandler
    : IRequestHandler<CreatePurchaseReturnCommand, Result<long>>
{
    private readonly IApplicationDbContext _db;
    private readonly IStockLedger _ledger;
    private readonly IDocumentNumberService _numbers;
    private readonly IDateTimeProvider _clock;

    public CreatePurchaseReturnCommandHandler(
        IApplicationDbContext db,
        IStockLedger ledger,
        IDocumentNumberService numbers,
        IDateTimeProvider clock)
    {
        _db = db;
        _ledger = ledger;
        _numbers = numbers;
        _clock = clock;
    }

    public async Task<Result<long>> Handle(CreatePurchaseReturnCommand request, CancellationToken cancellationToken)
    {
        PurchaseInvoice? invoice = await _db.PurchaseInvoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == request.PurchaseInvoiceId, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure<long>(Error.NotFound("فاتورة الشراء غير موجودة."));
        }

        if (invoice.Status == PurchaseInvoiceStatus.Cancelled)
        {
            return Result.Failure<long>(Error.Conflict("لا يمكن إرجاع فاتورة ملغاة."));
        }

        // Validate every requested line against the still-returnable quantity of its invoice line.
        var errors = new List<FieldError>();
        var plannedLines = new List<(PurchaseInvoiceItem Item, decimal Qty)>();
        foreach (CreatePurchaseReturnLine line in request.Items)
        {
            PurchaseInvoiceItem? invoiceItem = invoice.Items.FirstOrDefault(x => x.Id == line.PurchaseInvoiceItemId);
            if (invoiceItem is null)
            {
                errors.Add(new FieldError("items", $"البند {line.PurchaseInvoiceItemId} لا ينتمي لهذه الفاتورة."));
                continue;
            }

            decimal returnable = invoiceItem.Quantity - invoiceItem.ReturnedQty;
            if (line.Quantity > returnable)
            {
                errors.Add(new FieldError("items",
                    $"الكمية المرتجعة للبند {invoiceItem.Id} تتجاوز المتبقّي ({returnable})."));
                continue;
            }

            plannedLines.Add((invoiceItem, line.Quantity));
        }

        if (errors.Count > 0)
        {
            return Result.Failure<long>(Error.Validation("بيانات الإرجاع غير صالحة.", errors));
        }

        string number = await _numbers.NextAsync(DocumentType.PurchaseReturn, cancellationToken);

        var purchaseReturn = new PurchaseReturn
        {
            ReturnNumber = number,
            PurchaseInvoiceId = invoice.Id,
            WarehouseId = invoice.WarehouseId,
            ReturnDate = request.ReturnDate ?? _clock.UtcNow,
            Reason = request.Reason?.Trim(),
        };

        decimal totalAmount = 0m;
        foreach ((PurchaseInvoiceItem item, decimal qty) in plannedLines)
        {
            decimal lineTotal = qty * item.UnitPrice;
            totalAmount += lineTotal;

            purchaseReturn.Items.Add(new PurchaseReturnItem
            {
                PurchaseInvoiceItemId = item.Id,
                ProductId = item.ProductId,
                Quantity = qty,
                UnitPrice = item.UnitPrice,
                LineTotal = lineTotal,
            });

            item.ReturnedQty += qty;

            // Remove the returned goods from stock (outbound). Deducts at current average cost.
            Result apply = await _ledger.ApplyAsync(
                item.ProductId, invoice.WarehouseId, -qty, item.UnitPrice,
                StockMovementType.Out, reason: "Purchase return", referenceCode: number, cancellationToken);
            if (apply.IsFailure)
            {
                return Result.Failure<long>(apply.Error!);
            }
        }

        purchaseReturn.TotalAmount = totalAmount;
        _db.PurchaseReturns.Add(purchaseReturn);

        // Reduce payable by the returned value.
        Supplier? supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == invoice.SupplierId, cancellationToken);
        if (supplier is not null)
        {
            supplier.Balance -= totalAmount;
        }

        invoice.Status = invoice.Items.All(x => x.ReturnedQty >= x.Quantity)
            ? PurchaseInvoiceStatus.FullyReturned
            : PurchaseInvoiceStatus.PartiallyReturned;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(purchaseReturn.Id);
    }
}
