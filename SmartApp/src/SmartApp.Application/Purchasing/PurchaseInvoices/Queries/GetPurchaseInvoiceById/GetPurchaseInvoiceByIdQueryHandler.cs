using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Purchasing.PurchaseInvoices.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.PurchaseInvoices.Queries.GetPurchaseInvoiceById;

/// <summary>Loads one tenant purchase invoice with its items; NOT_FOUND if it isn't the caller's.</summary>
public sealed class GetPurchaseInvoiceByIdQueryHandler
    : IRequestHandler<GetPurchaseInvoiceByIdQuery, Result<PurchaseInvoiceDto>>
{
    private readonly IApplicationDbContext _db;

    public GetPurchaseInvoiceByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PurchaseInvoiceDto>> Handle(
        GetPurchaseInvoiceByIdQuery request, CancellationToken cancellationToken)
    {
        PurchaseInvoiceDto? invoice = await _db.PurchaseInvoices
            .Where(i => i.Id == request.InvoiceId)
            .Select(i => new PurchaseInvoiceDto(
                i.Id,
                i.InvoiceNumber,
                i.SupplierId,
                i.Supplier!.Name,
                i.WarehouseId,
                i.InvoiceDate,
                (byte)i.Status,
                i.SubTotal,
                i.DiscountTotal,
                i.TaxTotal,
                i.GrandTotal,
                i.PaidAmount,
                i.Notes,
                i.Items
                    .OrderBy(x => x.Id)
                    .Select(x => new PurchaseInvoiceItemDto(
                        x.Id, x.ProductId, x.Quantity, x.UnitPrice, x.DiscountAmount,
                        x.TaxRate, x.TaxAmount, x.LineTotal, x.ReturnedQty))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return invoice is null
            ? Result.Failure<PurchaseInvoiceDto>(Error.NotFound("فاتورة الشراء غير موجودة."))
            : Result.Success(invoice);
    }
}
