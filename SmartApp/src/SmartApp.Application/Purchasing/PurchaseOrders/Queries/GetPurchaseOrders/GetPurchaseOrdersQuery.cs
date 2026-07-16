using MediatR;
using SmartApp.Application.Purchasing.PurchaseOrders.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.PurchaseOrders.Queries.GetPurchaseOrders;

/// <summary>Lists the current tenant's purchase orders, optionally filtered by supplier.</summary>
public sealed record GetPurchaseOrdersQuery(long? SupplierId = null)
    : IRequest<Result<IReadOnlyList<PurchaseOrderListItemDto>>>;
