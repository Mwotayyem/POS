using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Sales.SalesInvoices.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.SalesInvoices.Queries.GetSalesInvoiceById;

/// <summary>Loads one tenant sales invoice with its items; NOT_FOUND if it isn't the caller's.</summary>
public sealed class GetSalesInvoiceByIdQueryHandler
    : IRequestHandler<GetSalesInvoiceByIdQuery, Result<SalesInvoiceDto>>
{
    private readonly IApplicationDbContext _db;

    public GetSalesInvoiceByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<SalesInvoiceDto>> Handle(
        GetSalesInvoiceByIdQuery request, CancellationToken cancellationToken)
    {
        SalesInvoiceDto? invoice = await _db.SalesInvoices
            .Where(i => i.Id == request.InvoiceId)
            .Select(i => new SalesInvoiceDto(
                i.Id,
                i.InvoiceNumber,
                i.CustomerId,
                i.Customer != null ? i.Customer.Name : null,
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
                    .Select(x => new SalesInvoiceItemDto(
                        x.Id, x.ProductId, x.Quantity, x.UnitPrice, x.UnitCost, x.DiscountAmount,
                        x.TaxRate, x.TaxAmount, x.LineTotal, x.ReturnedQty))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return invoice is null
            ? Result.Failure<SalesInvoiceDto>(Error.NotFound("فاتورة المبيعات غير موجودة."))
            : Result.Success(invoice);
    }
}
