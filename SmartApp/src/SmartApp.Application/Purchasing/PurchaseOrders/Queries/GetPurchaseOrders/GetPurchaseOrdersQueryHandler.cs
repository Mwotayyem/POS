using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Purchasing.PurchaseOrders.Dtos;
using SmartApp.Domain.Purchasing;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.PurchaseOrders.Queries.GetPurchaseOrders;

/// <summary>Loads the current tenant's purchase orders (tenant-filtered), newest first.</summary>
public sealed class GetPurchaseOrdersQueryHandler
    : IRequestHandler<GetPurchaseOrdersQuery, Result<IReadOnlyList<PurchaseOrderListItemDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetPurchaseOrdersQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<PurchaseOrderListItemDto>>> Handle(
        GetPurchaseOrdersQuery request, CancellationToken cancellationToken)
    {
        IQueryable<PurchaseOrder> query = _db.PurchaseOrders;

        if (request.SupplierId is long supplierId)
        {
            query = query.Where(o => o.SupplierId == supplierId);
        }

        List<PurchaseOrderListItemDto> orders = await query
            .OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.Id)
            .Select(o => new PurchaseOrderListItemDto(
                o.Id, o.OrderNumber, o.SupplierId, o.Supplier!.Name, o.OrderDate, (byte)o.Status, o.Total))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PurchaseOrderListItemDto>>(orders);
    }
}
