using MediatR;
using SmartApp.Application.Purchasing.PurchaseReturns.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.PurchaseReturns.Queries.GetPurchaseReturnById;

/// <summary>Fetches one purchase return of the current tenant with its lines. NOT_FOUND if absent.</summary>
public sealed record GetPurchaseReturnByIdQuery(long ReturnId) : IRequest<Result<PurchaseReturnDto>>;
