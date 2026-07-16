using MediatR;
using SmartApp.Application.Purchasing.PurchaseOrders.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderById;

/// <summary>Fetches one purchase order of the current tenant with its lines. NOT_FOUND if absent.</summary>
public sealed record GetPurchaseOrderByIdQuery(long OrderId) : IRequest<Result<PurchaseOrderDto>>;
