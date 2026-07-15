using MediatR;
using SmartApp.Application.Catalog.Products.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Products.Queries.GetProductById;

/// <summary>Fetches one product of the current tenant by id, with its child collections. NOT_FOUND if absent.</summary>
public sealed record GetProductByIdQuery(long ProductId) : IRequest<Result<ProductDto>>;
