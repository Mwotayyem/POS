using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Inventory.Enums;
using SmartApp.Domain.Purchasing;
using SmartApp.Domain.Purchasing.Enums;
using SmartApp.Domain.Sequences;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.PurchaseInvoices.Commands.CreatePurchaseInvoice;

/// <summary>
/// Posts a purchase invoice atomically. Validates references, computes line and header totals,
/// reserves a number, receives stock through <see cref="IStockLedger"/> (inbound → WAC recompute +
/// append-only movement), and increases the supplier's payable balance. A single SaveChanges commits
/// the invoice, its items, the stock changes, the movements, the supplier balance, and the sequence.
/// </summary>
public sealed class CreatePurchaseInvoiceCommandHandler
    : IRequestHandler<CreatePurchaseInvoiceCommand, Result<long>>
{
    private readonly IApplicationDbContext _db;
    private readonly IStockLedger _ledger;
    private readonly IDocumentNumberService _numbers;
    private readonly IDateTimeProvider _clock;

    public CreatePurchaseInvoiceCommandHandler(
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

    public async Task<Result<long>> Handle(CreatePurchaseInvoiceCommand request, CancellationToken cancellationToken)
    {
        Supplier? supplier = await _db.Suppliers
            .FirstOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure<long>(Error.Validation(
                "المورّد غير موجود.", [new FieldError("supplierId", "معرّف مورّد غير صالح.")]));
        }

        if (!await _db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, cancellationToken))
        {
            return Result.Failure<long>(Error.Validation(
                "المستودع غير موجود.", [new FieldError("warehouseId", "معرّف مستودع غير صالح.")]));
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        int foundProducts = await _db.Products.CountAsync(p => productIds.Contains(p.Id), cancellationToken);
        if (foundProducts != productIds.Count)
        {
            return Result.Failure<long>(Error.Validation(
                "منتج واحد أو أكثر غير موجود.", [new FieldError("items", "منتج غير صالح.")]));
        }

        string number = await _numbers.NextAsync(DocumentType.PurchaseInvoice, cancellationToken);

        var invoice = new PurchaseInvoice
        {
            InvoiceNumber = number,
            SupplierId = request.SupplierId,
            WarehouseId = request.WarehouseId,
            InvoiceDate = request.InvoiceDate ?? _clock.UtcNow,
            Status = PurchaseInvoiceStatus.Confirmed,
            Notes = request.Notes?.Trim(),
        };

        decimal subTotal = 0m, discountTotal = 0m, taxTotal = 0m, grandTotal = 0m;

        foreach (CreatePurchaseInvoiceLine line in request.Items)
        {
            decimal gross = line.Quantity * line.UnitPrice;
            decimal net = gross - line.DiscountAmount;
            decimal taxAmount = decimal.Round(net * line.TaxRate / 100m, 4, MidpointRounding.AwayFromZero);
            decimal lineTotal = net + taxAmount;

            subTotal += gross;
            discountTotal += line.DiscountAmount;
            taxTotal += taxAmount;
            grandTotal += lineTotal;

            invoice.Items.Add(new PurchaseInvoiceItem
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                DiscountAmount = line.DiscountAmount,
                TaxRate = line.TaxRate,
                TaxAmount = taxAmount,
                LineTotal = lineTotal,
                ReturnedQty = 0m,
            });
        }

        invoice.SubTotal = subTotal;
        invoice.DiscountTotal = discountTotal;
        invoice.TaxTotal = taxTotal;
        invoice.GrandTotal = grandTotal;
        invoice.PaidAmount = request.PaidAmount;

        _db.PurchaseInvoices.Add(invoice);

        // Receive stock for each line (inbound → WAC). The ledger stages Stock + StockMovement changes.
        foreach (CreatePurchaseInvoiceLine line in request.Items)
        {
            Result apply = await _ledger.ApplyAsync(
                line.ProductId, request.WarehouseId, line.Quantity, line.UnitPrice,
                StockMovementType.In, reason: "Purchase invoice", referenceCode: number, cancellationToken);
            if (apply.IsFailure)
            {
                return Result.Failure<long>(apply.Error!);
            }
        }

        // Increase payable by the unpaid portion of this invoice.
        supplier.Balance += grandTotal - request.PaidAmount;

        // Mark a source purchase order as received, if provided.
        if (request.PurchaseOrderId is long orderId)
        {
            PurchaseOrder? order = await _db.PurchaseOrders
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
            if (order is not null)
            {
                order.Status = PurchaseOrderStatus.Received;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(invoice.Id);
    }
}
