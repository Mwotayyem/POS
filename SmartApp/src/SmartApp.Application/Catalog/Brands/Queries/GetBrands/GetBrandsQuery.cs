using MediatR;
using SmartApp.Application.Catalog.Brands.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Brands.Queries.GetBrands;

/// <summary>Lists the current tenant's brands, ordered by name.</summary>
public sealed record GetBrandsQuery : IRequest<Result<IReadOnlyList<BrandDto>>>;
