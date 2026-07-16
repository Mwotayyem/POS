using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.PurchaseOrders.Commands.CreatePurchaseOrder;

/// <summary>
/// Creates a draft purchase order for a supplier. Does not affect stock. Assigns a number and
/// computes the total. Returns the new order id.
/// </summary>
public sealed record CreatePurchaseOrderCommand(
    long SupplierId,
    DateTime? OrderDate,
    string? Notes,
    IReadOnlyList<CreatePurchaseOrderLine> Items) : IRequest<Result<long>>;

/// <summary>A line on the new purchase order.</summary>
public sealed record CreatePurchaseOrderLine(long ProductId, decimal Quantity, decimal UnitCost);
