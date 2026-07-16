using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SmartApp.API.Authorization;
using SmartApp.Application.Purchasing.PurchaseOrders.Commands.ChangePurchaseOrderStatus;
using SmartApp.Application.Purchasing.PurchaseOrders.Commands.CreatePurchaseOrder;
using SmartApp.Application.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderById;
using SmartApp.Application.Purchasing.PurchaseOrders.Queries.GetPurchaseOrders;
using SmartApp.Shared.Constants;

namespace SmartApp.API.Controllers.v1;

/// <summary>
/// Purchase orders (plan a purchase; no stock effect). Create + list/get + status workflow
/// (confirm/cancel). Guarded by purchasing permissions. Receiving happens via a purchase invoice.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/purchase-orders")]
public sealed class PurchaseOrdersController : ApiControllerBase
{
    /// <summary>Lists purchase orders, optionally filtered by supplier.</summary>
    [HttpGet]
    [HasPermission(Permissions.Purchases.View)]
    public async Task<IActionResult> GetOrders([FromQuery] long? supplierId, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetPurchaseOrdersQuery(supplierId), cancellationToken));

    /// <summary>Gets a purchase order with its lines.</summary>
    [HttpGet("{id:long}")]
    [HasPermission(Permissions.Purchases.View)]
    public async Task<IActionResult> GetOrder(long id, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetPurchaseOrderByIdQuery(id), cancellationToken));

    /// <summary>Creates a draft purchase order. Returns the new id.</summary>
    [HttpPost]
    [HasPermission(Permissions.Purchases.Create)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new CreatePurchaseOrderCommand(
                request.SupplierId, request.OrderDate, request.Notes,
                request.Items ?? []),
            cancellationToken));

    /// <summary>Confirms a draft purchase order.</summary>
    [HttpPost("{id:long}/confirm")]
    [HasPermission(Permissions.Purchases.Update)]
    public async Task<IActionResult> ConfirmOrder(long id, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new ChangePurchaseOrderStatusCommand(id, PurchaseOrderAction.Confirm), cancellationToken));

    /// <summary>Cancels a purchase order.</summary>
    [HttpPost("{id:long}/cancel")]
    [HasPermission(Permissions.Purchases.Update)]
    public async Task<IActionResult> CancelOrder(long id, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new ChangePurchaseOrderStatusCommand(id, PurchaseOrderAction.Cancel), cancellationToken));
}

/// <summary>Create-purchase-order request body.</summary>
public sealed record CreatePurchaseOrderRequest(
    long SupplierId,
    DateTime? OrderDate,
    string? Notes,
    IReadOnlyList<CreatePurchaseOrderLine>? Items);
