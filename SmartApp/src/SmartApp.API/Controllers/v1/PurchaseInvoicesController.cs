using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SmartApp.API.Authorization;
using SmartApp.Application.Purchasing.PurchaseInvoices.Commands.CreatePurchaseInvoice;
using SmartApp.Application.Purchasing.PurchaseInvoices.Queries.GetPurchaseInvoiceById;
using SmartApp.Application.Purchasing.PurchaseInvoices.Queries.GetPurchaseInvoices;
using SmartApp.Application.Purchasing.PurchaseReturns.Commands.CreatePurchaseReturn;
using SmartApp.Application.Purchasing.PurchaseReturns.Queries.GetPurchaseReturnById;
using SmartApp.Shared.Constants;

namespace SmartApp.API.Controllers.v1;

/// <summary>
/// Purchase invoices and their returns. Creating an invoice receives stock (inbound movement + WAC)
/// and increases the supplier balance atomically; creating a return reverses stock and balance.
/// Guarded by purchasing permissions. See SmartApp-Architecture/13-Development-Rules.md §6.4.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/purchase-invoices")]
public sealed class PurchaseInvoicesController : ApiControllerBase
{
    /// <summary>Lists purchase invoices, optionally filtered by supplier.</summary>
    [HttpGet]
    [HasPermission(Permissions.Purchases.View)]
    public async Task<IActionResult> GetInvoices([FromQuery] long? supplierId, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetPurchaseInvoicesQuery(supplierId), cancellationToken));

    /// <summary>Gets a purchase invoice with its lines.</summary>
    [HttpGet("{id:long}")]
    [HasPermission(Permissions.Purchases.View)]
    public async Task<IActionResult> GetInvoice(long id, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetPurchaseInvoiceByIdQuery(id), cancellationToken));

    /// <summary>Creates and posts a purchase invoice (receives stock + updates balance). Returns the new id.</summary>
    [HttpPost]
    [HasPermission(Permissions.Purchases.Create)]
    public async Task<IActionResult> CreateInvoice(
        [FromBody] CreatePurchaseInvoiceRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new CreatePurchaseInvoiceCommand(
                request.SupplierId, request.WarehouseId, request.InvoiceDate, request.PaidAmount,
                request.Notes, request.PurchaseOrderId, request.Items ?? []),
            cancellationToken));

    /// <summary>Gets a purchase return with its lines.</summary>
    [HttpGet("returns/{returnId:long}")]
    [HasPermission(Permissions.Purchases.View)]
    public async Task<IActionResult> GetReturn(long returnId, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetPurchaseReturnByIdQuery(returnId), cancellationToken));

    /// <summary>Creates a purchase return against the given invoice (reverses stock + balance).</summary>
    [HttpPost("{id:long}/returns")]
    [HasPermission(Permissions.Purchases.Post)]
    public async Task<IActionResult> CreateReturn(
        long id, [FromBody] CreatePurchaseReturnRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new CreatePurchaseReturnCommand(id, request.ReturnDate, request.Reason, request.Items ?? []),
            cancellationToken));
}

/// <summary>Create-purchase-invoice request body.</summary>
public sealed record CreatePurchaseInvoiceRequest(
    long SupplierId,
    long WarehouseId,
    DateTime? InvoiceDate,
    decimal PaidAmount,
    string? Notes,
    long? PurchaseOrderId,
    IReadOnlyList<CreatePurchaseInvoiceLine>? Items);

/// <summary>Create-purchase-return request body.</summary>
public sealed record CreatePurchaseReturnRequest(
    DateTime? ReturnDate,
    string? Reason,
    IReadOnlyList<CreatePurchaseReturnLine>? Items);
