namespace SmartApp.Application.Sales.Payments.Dtos;

/// <summary>A customer payment as returned by the API.</summary>
public sealed record PaymentDto(
    long Id,
    string PaymentNumber,
    long CustomerId,
    long? SalesInvoiceId,
    decimal Amount,
    DateTime PaymentDate,
    byte Method,
    string? Reference,
    string? Notes);
