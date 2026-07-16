using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.Customers.Commands.CreateCustomer;

/// <summary>Creates a customer in the current tenant. Balance starts at 0. Returns the new id.</summary>
public sealed record CreateCustomerCommand(
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    decimal CreditLimit) : IRequest<Result<long>>;
