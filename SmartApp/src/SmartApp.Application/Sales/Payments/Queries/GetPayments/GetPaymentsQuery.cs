using MediatR;
using SmartApp.Application.Sales.Payments.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.Payments.Queries.GetPayments;

/// <summary>Lists the current tenant's payments, optionally filtered by customer.</summary>
public sealed record GetPaymentsQuery(long? CustomerId = null) : IRequest<Result<IReadOnlyList<PaymentDto>>>;
