using MediatR;
using SmartApp.Application.Sales.SalesReturns.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.SalesReturns.Queries.GetSalesReturnById;

/// <summary>Fetches one sales return of the current tenant with its lines. NOT_FOUND if absent.</summary>
public sealed record GetSalesReturnByIdQuery(long ReturnId) : IRequest<Result<SalesReturnDto>>;
