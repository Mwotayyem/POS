using MediatR;
using SmartApp.Application.Catalog.Brands.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Brands.Queries.GetBrandById;

/// <summary>Fetches one brand of the current tenant by id. NOT_FOUND if absent.</summary>
public sealed record GetBrandByIdQuery(long BrandId) : IRequest<Result<BrandDto>>;
