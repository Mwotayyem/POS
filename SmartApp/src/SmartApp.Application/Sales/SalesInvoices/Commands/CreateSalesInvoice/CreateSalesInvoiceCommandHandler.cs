using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Inventory.Enums;
using SmartApp.Domain.Sales;
using SmartApp.Domain.Sales.Enums;
using SmartApp.Domain.Sequences;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.SalesInvoices.Commands.CreateSalesInvoice;

/// <summary>
/// Posts a sales invoice atomically. Validates the customer (if any), warehouse, and products;
/// computes line and header totals; snapshots each line's cost from the current stock average (for
/// profitability); issues stock through <see cref="IStockLedger"/> (outbound — rejected if it would
/// go negative); and increases the customer's receivable balance by the unpaid amount. A single
/// SaveChanges commits the invoice, items, stock changes, movements, customer balance, and sequence.
/// </summary>
public sealed class CreateSalesInvoiceCommandHandler
    : IRequestHandler<CreateSalesInvoiceCommand, Result<long>>
{
    private readonly IApplicationDbContext _db;
    private readonly IStockLedger _ledger;
    private readonly IDocumentNumberService _numbers;
    private readonly IDateTimeProvider _clock;

    public CreateSalesInvoiceCommandHandler(
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

    public async Task<Result<long>> Handle(CreateSalesInvoiceCommand request, CancellationToken cancellationToken)
    {
        Customer? customer = null;
        if (request.CustomerId is long customerId)
        {
            customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
            if (customer is null)
            {
                return Result.Failure<long>(Error.Validation(
                    "العميل غير موجود.", [new FieldError("customerId", "معرّف عميل غير صالح.")]));
            }
        }

        if (!await _db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, cancellationToken))
        {
            return Result.Failure<long>(Error.Validation(
                "المستودع غير موجود.", [new FieldError("warehouseId", "معرّف مستودع غير صالح.")]));
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        if (await _db.Products.CountAsync(p => productIds.Contains(p.Id), cancellationToken) != productIds.Count)
        {
            return Result.Failure<long>(Error.Validation(
                "منتج واحد أو أكثر غير موجود.", [new FieldError("items", "منتج غير صالح.")]));
        }

        string number = await _numbers.NextAsync(DocumentType.SalesInvoice, cancellationToken);

        var invoice = new SalesInvoice
        {
            InvoiceNumber = number,
            CustomerId = request.CustomerId,
            WarehouseId = request.WarehouseId,
            InvoiceDate = request.InvoiceDate ?? _clock.UtcNow,
            Status = SalesInvoiceStatus.Confirmed,
            Notes = request.Notes?.Trim(),
        };

        decimal subTotal = 0m, discountTotal = 0m, taxTotal = 0m, grandTotal = 0m;

        foreach (CreateSalesInvoiceLine line in request.Items)
        {
            // Snapshot the current weighted-average cost for profitability before issuing stock.
            decimal unitCost = await _db.Stocks
                .Where(s => s.ProductId == line.ProductId && s.WarehouseId == request.WarehouseId)
                .Select(s => (decimal?)s.AvgCost)
                .FirstOrDefaultAsync(cancellationToken) ?? 0m;

            decimal gross = line.Quantity * line.UnitPrice;
            decimal net = gross - line.DiscountAmount;
            decimal taxAmount = decimal.Round(net * line.TaxRate / 100m, 4, MidpointRounding.AwayFromZero);
            decimal lineTotal = net + taxAmount;

            subTotal += gross;
            discountTotal += line.DiscountAmount;
            taxTotal += taxAmount;
            grandTotal += lineTotal;

            invoice.Items.Add(new SalesInvoiceItem
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                UnitCost = unitCost,
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

        _db.SalesInvoices.Add(invoice);

        // Issue stock for each line (outbound). The ledger rejects if a product lacks quantity.
        foreach (CreateSalesInvoiceLine line in request.Items)
        {
            Result apply = await _ledger.ApplyAsync(
                line.ProductId, request.WarehouseId, -line.Quantity, unitCost: 0m,
                StockMovementType.Out, reason: "Sales invoice", referenceCode: number, cancellationToken);
            if (apply.IsFailure)
            {
                return Result.Failure<long>(apply.Error!);
            }
        }

        // Increase receivable by the unpaid portion of this invoice (only for registered customers).
        if (customer is not null)
        {
            customer.Balance += grandTotal - request.PaidAmount;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(invoice.Id);
    }
}
