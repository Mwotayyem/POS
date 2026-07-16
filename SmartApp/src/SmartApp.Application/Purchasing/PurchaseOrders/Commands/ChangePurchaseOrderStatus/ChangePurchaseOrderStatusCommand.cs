using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.PurchaseOrders.Commands.ChangePurchaseOrderStatus;

/// <summary>
/// Transitions a purchase order's status. Allowed: Confirm (Draft → Confirmed) and Cancel
/// (Draft/Confirmed → Cancelled). Received is set only by creating an invoice from the order.
/// </summary>
public sealed record ChangePurchaseOrderStatusCommand(long OrderId, PurchaseOrderAction Action) : IRequest<Result>;

/// <summary>The requested transition.</summary>
public enum PurchaseOrderAction
{
    Confirm = 1,
    Cancel = 2,
}
