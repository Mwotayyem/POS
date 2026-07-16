using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SmartApp.API.Authorization;
using SmartApp.Application.Sales.Payments.Commands.RecordPayment;
using SmartApp.Application.Sales.Payments.Queries.GetPayments;
using SmartApp.Shared.Constants;

namespace SmartApp.API.Controllers.v1;

/// <summary>
/// Customer payments (debt settlements — not a payment gateway). Recording a payment reduces the
/// customer balance and, when linked, the invoice's outstanding amount. Guarded by sales permissions.
/// </summary>
[ApiVersion("1.0")]
public sealed class PaymentsController : ApiControllerBase
{
    /// <summary>Lists payments, optionally filtered by customer.</summary>
    [HttpGet]
    [HasPermission(Permissions.Sales.View)]
    public async Task<IActionResult> GetPayments([FromQuery] long? customerId, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetPaymentsQuery(customerId), cancellationToken));

    /// <summary>Records a customer payment. Returns the new id.</summary>
    [HttpPost]
    [HasPermission(Permissions.Sales.Post)]
    public async Task<IActionResult> RecordPayment(
        [FromBody] RecordPaymentRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new RecordPaymentCommand(
                request.CustomerId, request.SalesInvoiceId, request.Amount, request.PaymentDate,
                request.Method, request.Reference, request.Notes),
            cancellationToken));
}

/// <summary>Record-payment request body. Method: 1=Cash, 2=Transfer, 3=Card.</summary>
public sealed record RecordPaymentRequest(
    long CustomerId,
    long? SalesInvoiceId,
    decimal Amount,
    DateTime? PaymentDate,
    byte Method,
    string? Reference,
    string? Notes);
