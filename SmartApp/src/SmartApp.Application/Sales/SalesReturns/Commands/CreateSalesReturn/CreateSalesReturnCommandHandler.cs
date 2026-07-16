using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Inventory.Enums;
using SmartApp.Domain.Sales;
using SmartApp.Domain.Sales.Enums;
using SmartApp.Domain.Sequences;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.SalesReturns.Commands.CreateSalesReturn;

/// <summary>
/// Posts a sales return atomically. For each line it checks the requested quantity does not exceed
/// the still-returnable quantity (Quantity − ReturnedQty), restocks that quantity through
/// <see cref="IStockLedger"/> (inbound at the line's original cost), freezes the original prices,
/// bumps ReturnedQty, and reduces the customer's receivable balance. Finally it recomputes the
/// invoice status (Confirmed → PartiallyReturned → FullyReturned). One SaveChanges commits everything.
/// </summary>
public sealed class CreateSalesReturnCommandHandler
    : IRequestHandler<CreateSalesReturnCommand, Result<long>>
{
    private readonly IApplicationDbContext _db;
    private readonly IStockLedger _ledger;
    private readonly IDocumentNumberService _numbers;
    private readonly IDateTimeProvider _clock;

    public CreateSalesReturnCommandHandler(
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

    public async Task<Result<long>> Handle(CreateSalesReturnCommand request, CancellationToken cancellationToken)
    {
        SalesInvoice? invoice = await _db.SalesInvoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == request.SalesInvoiceId, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure<long>(Error.NotFound("فاتورة المبيعات غير موجودة."));
        }

        if (invoice.Status == SalesInvoiceStatus.Cancelled)
        {
            return Result.Failure<long>(Error.Conflict("لا يمكن إرجاع فاتورة ملغاة."));
        }

        var errors = new List<FieldError>();
        var plannedLines = new List<(SalesInvoiceItem Item, decimal Qty)>();
        foreach (CreateSalesReturnLine line in request.Items)
        {
            SalesInvoiceItem? invoiceItem = invoice.Items.FirstOrDefault(x => x.Id == line.SalesInvoiceItemId);
            if (invoiceItem is null)
            {
                errors.Add(new FieldError("items", $"البند {line.SalesInvoiceItemId} لا ينتمي لهذه الفاتورة."));
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

        string number = await _numbers.NextAsync(DocumentType.SalesReturn, cancellationToken);

        var salesReturn = new SalesReturn
        {
            ReturnNumber = number,
            SalesInvoiceId = invoice.Id,
            WarehouseId = invoice.WarehouseId,
            ReturnDate = request.ReturnDate ?? _clock.UtcNow,
            Reason = request.Reason?.Trim(),
        };

        decimal totalAmount = 0m;
        foreach ((SalesInvoiceItem item, decimal qty) in plannedLines)
        {
            decimal lineTotal = qty * item.UnitPrice;
            totalAmount += lineTotal;

            salesReturn.Items.Add(new SalesReturnItem
            {
                SalesInvoiceItemId = item.Id,
                ProductId = item.ProductId,
                Quantity = qty,
                UnitPrice = item.UnitPrice,
                UnitCost = item.UnitCost,
                LineTotal = lineTotal,
            });

            item.ReturnedQty += qty;

            // Restock the returned goods (inbound) at the original cost.
            Result apply = await _ledger.ApplyAsync(
                item.ProductId, invoice.WarehouseId, qty, item.UnitCost,
                StockMovementType.In, reason: "Sales return", referenceCode: number, cancellationToken);
            if (apply.IsFailure)
            {
                return Result.Failure<long>(apply.Error!);
            }
        }

        salesReturn.TotalAmount = totalAmount;
        _db.SalesReturns.Add(salesReturn);

        // Reduce receivable by the returned value (only for registered customers).
        if (invoice.CustomerId is long customerId)
        {
            Customer? customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
            if (customer is not null)
            {
                customer.Balance -= totalAmount;
            }
        }

        invoice.Status = invoice.Items.All(x => x.ReturnedQty >= x.Quantity)
            ? SalesInvoiceStatus.FullyReturned
            : SalesInvoiceStatus.PartiallyReturned;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(salesReturn.Id);
    }
}
