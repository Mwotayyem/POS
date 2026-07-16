using MediatR;
using SmartApp.Application.Sales.Customers.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.Customers.Queries.GetCustomerById;

/// <summary>Fetches one customer of the current tenant by id. NOT_FOUND if absent.</summary>
public sealed record GetCustomerByIdQuery(long CustomerId) : IRequest<Result<CustomerDto>>;
