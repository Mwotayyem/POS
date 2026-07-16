using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Sales.SalesInvoices.Dtos;
using SmartApp.Domain.Sales;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.SalesInvoices.Queries.GetSalesInvoices;

/// <summary>Loads the current tenant's sales invoices (tenant-filtered), newest first.</summary>
public sealed class GetSalesInvoicesQueryHandler
    : IRequestHandler<GetSalesInvoicesQuery, Result<IReadOnlyList<SalesInvoiceListItemDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetSalesInvoicesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<SalesInvoiceListItemDto>>> Handle(
        GetSalesInvoicesQuery request, CancellationToken cancellationToken)
    {
        IQueryable<SalesInvoice> query = _db.SalesInvoices;

        if (request.CustomerId is long customerId)
        {
            query = query.Where(i => i.CustomerId == customerId);
        }

        List<SalesInvoiceListItemDto> invoices = await query
            .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id)
            .Select(i => new SalesInvoiceListItemDto(
                i.Id,
                i.InvoiceNumber,
                i.CustomerId,
                i.Customer != null ? i.Customer.Name : null,
                i.WarehouseId,
                i.InvoiceDate,
                (byte)i.Status,
                i.GrandTotal,
                i.PaidAmount))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SalesInvoiceListItemDto>>(invoices);
    }
}
