using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.Customers.Commands.DeleteCustomer;

/// <summary>Soft-deletes a customer. Refused if any sales invoice or payment references it.</summary>
public sealed record DeleteCustomerCommand(long CustomerId) : IRequest<Result>;
