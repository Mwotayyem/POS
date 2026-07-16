using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.Payments.Commands.RecordPayment;

/// <summary>
/// Records a customer payment (debt settlement): assigns a number, reduces the customer's receivable
/// balance, and — if linked to an invoice — increases that invoice's paid amount. One transaction.
/// Returns the new payment id. <see cref="Method"/> is the numeric PaymentMethod (1=Cash,2=Transfer,3=Card).
/// </summary>
public sealed record RecordPaymentCommand(
    long CustomerId,
    long? SalesInvoiceId,
    decimal Amount,
    DateTime? PaymentDate,
    byte Method,
    string? Reference,
    string? Notes) : IRequest<Result<long>>;
