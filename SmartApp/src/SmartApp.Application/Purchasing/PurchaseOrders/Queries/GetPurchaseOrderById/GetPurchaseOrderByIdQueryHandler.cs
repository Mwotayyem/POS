using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Purchasing.PurchaseOrders.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderById;

/// <summary>Loads one tenant purchase order with its items; NOT_FOUND if it isn't the caller's.</summary>
public sealed class GetPurchaseOrderByIdQueryHandler
    : IRequestHandler<GetPurchaseOrderByIdQuery, Result<PurchaseOrderDto>>
{
    private readonly IApplicationDbContext _db;

    public GetPurchaseOrderByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PurchaseOrderDto>> Handle(
        GetPurchaseOrderByIdQuery request, CancellationToken cancellationToken)
    {
        PurchaseOrderDto? order = await _db.PurchaseOrders
            .Where(o => o.Id == request.OrderId)
            .Select(o => new PurchaseOrderDto(
                o.Id,
                o.OrderNumber,
                o.SupplierId,
                o.Supplier!.Name,
                o.OrderDate,
                (byte)o.Status,
                o.Total,
                o.Notes,
                o.Items
                    .OrderBy(x => x.Id)
                    .Select(x => new PurchaseOrderItemDto(x.Id, x.ProductId, x.Quantity, x.UnitCost, x.LineTotal))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return order is null
            ? Result.Failure<PurchaseOrderDto>(Error.NotFound("أمر الشراء غير موجود."))
            : Result.Success(order);
    }
}
