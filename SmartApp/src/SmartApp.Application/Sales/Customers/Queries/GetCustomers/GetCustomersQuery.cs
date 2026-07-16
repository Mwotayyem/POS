using MediatR;
using SmartApp.Application.Sales.Customers.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.Customers.Queries.GetCustomers;

/// <summary>Lists the current tenant's customers, ordered by name, with optional name/phone search.</summary>
public sealed record GetCustomersQuery(string? Search = null) : IRequest<Result<IReadOnlyList<CustomerDto>>>;
