using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.Customers.Commands.UpdateCustomer;

/// <summary>Updates a customer's details. Balance is system-managed and not settable here.</summary>
public sealed record UpdateCustomerCommand(
    long CustomerId,
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    decimal CreditLimit,
    bool IsActive) : IRequest<Result>;
