using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SmartApp.API.Authorization;
using SmartApp.Application.Sales.SalesInvoices.Commands.CreateSalesInvoice;
using SmartApp.Application.Sales.SalesInvoices.Queries.GetSalesInvoiceById;
using SmartApp.Application.Sales.SalesInvoices.Queries.GetSalesInvoices;
using SmartApp.Application.Sales.SalesReturns.Commands.CreateSalesReturn;
using SmartApp.Application.Sales.SalesReturns.Queries.GetSalesReturnById;
using SmartApp.Shared.Constants;

namespace SmartApp.API.Controllers.v1;

/// <summary>
/// Sales invoices and their returns. Creating an invoice issues stock (outbound movement, cost
/// snapshot) and increases the customer balance atomically; creating a return restocks and reduces
/// the balance. Guarded by sales permissions. See SmartApp-Architecture/13-Development-Rules.md §6.4.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/sales-invoices")]
public sealed class SalesInvoicesController : ApiControllerBase
{
    /// <summary>Lists sales invoices, optionally filtered by customer.</summary>
    [HttpGet]
    [HasPermission(Permissions.Sales.View)]
    public async Task<IActionResult> GetInvoices([FromQuery] long? customerId, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetSalesInvoicesQuery(customerId), cancellationToken));

    /// <summary>Gets a sales invoice with its lines.</summary>
    [HttpGet("{id:long}")]
    [HasPermission(Permissions.Sales.View)]
    public async Task<IActionResult> GetInvoice(long id, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetSalesInvoiceByIdQuery(id), cancellationToken));

    /// <summary>Creates and posts a sales invoice (issues stock + updates balance). Returns the new id.</summary>
    [HttpPost]
    [HasPermission(Permissions.Sales.Create)]
    public async Task<IActionResult> CreateInvoice(
        [FromBody] CreateSalesInvoiceRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new CreateSalesInvoiceCommand(
                request.CustomerId, request.WarehouseId, request.InvoiceDate, request.PaidAmount,
                request.Notes, request.Items ?? []),
            cancellationToken));

    /// <summary>Gets a sales return with its lines.</summary>
    [HttpGet("returns/{returnId:long}")]
    [HasPermission(Permissions.Sales.View)]
    public async Task<IActionResult> GetReturn(long returnId, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetSalesReturnByIdQuery(returnId), cancellationToken));

    /// <summary>Creates a sales return against the given invoice (restocks + reduces balance).</summary>
    [HttpPost("{id:long}/returns")]
    [HasPermission(Permissions.Sales.Post)]
    public async Task<IActionResult> CreateReturn(
        long id, [FromBody] CreateSalesReturnRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new CreateSalesReturnCommand(id, request.ReturnDate, request.Reason, request.Items ?? []),
            cancellationToken));
}

/// <summary>Create-sales-invoice request body.</summary>
public sealed record CreateSalesInvoiceRequest(
    long? CustomerId,
    long WarehouseId,
    DateTime? InvoiceDate,
    decimal PaidAmount,
    string? Notes,
    IReadOnlyList<CreateSalesInvoiceLine>? Items);

/// <summary>Create-sales-return request body.</summary>
public sealed record CreateSalesReturnRequest(
    DateTime? ReturnDate,
    string? Reason,
    IReadOnlyList<CreateSalesReturnLine>? Items);
