using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Purchasing.PurchaseInvoices.Dtos;
using SmartApp.Domain.Purchasing;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.PurchaseInvoices.Queries.GetPurchaseInvoices;

/// <summary>Loads the current tenant's purchase invoices (tenant-filtered), newest first.</summary>
public sealed class GetPurchaseInvoicesQueryHandler
    : IRequestHandler<GetPurchaseInvoicesQuery, Result<IReadOnlyList<PurchaseInvoiceListItemDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetPurchaseInvoicesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<PurchaseInvoiceListItemDto>>> Handle(
        GetPurchaseInvoicesQuery request, CancellationToken cancellationToken)
    {
        IQueryable<PurchaseInvoice> query = _db.PurchaseInvoices;

        if (request.SupplierId is long supplierId)
        {
            query = query.Where(i => i.SupplierId == supplierId);
        }

        List<PurchaseInvoiceListItemDto> invoices = await query
            .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id)
            .Select(i => new PurchaseInvoiceListItemDto(
                i.Id,
                i.InvoiceNumber,
                i.SupplierId,
                i.Supplier!.Name,
                i.WarehouseId,
                i.InvoiceDate,
                (byte)i.Status,
                i.GrandTotal,
                i.PaidAmount))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PurchaseInvoiceListItemDto>>(invoices);
    }
}
