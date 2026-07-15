using MediatR;
using SmartApp.Application.Catalog.Products.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Products.Queries.GetProducts;

/// <summary>
/// Lists the current tenant's products with optional name/SKU search and category/brand filters.
/// Tenant scoping is enforced by the global query filter.
/// </summary>
public sealed record GetProductsQuery(
    string? Search = null,
    long? CategoryId = null,
    long? BrandId = null) : IRequest<Result<IReadOnlyList<ProductListItemDto>>>;
