using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Catalog.Products.Dtos;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Catalog;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Products.Queries.GetProducts;

/// <summary>Loads the current tenant's products (tenant-filtered), applying optional filters.</summary>
public sealed class GetProductsQueryHandler
    : IRequestHandler<GetProductsQuery, Result<IReadOnlyList<ProductListItemDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetProductsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<ProductListItemDto>>> Handle(
        GetProductsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Product> query = _db.Products;

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string term = request.Search.Trim();
            string like = $"%{term}%";
            query = query.Where(p => EF.Functions.Like(p.Name, like) ||
                                     (p.Sku != null && EF.Functions.Like(p.Sku, like)));
        }

        if (request.CategoryId is long categoryId)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }

        if (request.BrandId is long brandId)
        {
            query = query.Where(p => p.BrandId == brandId);
        }

        List<ProductListItemDto> products = await query
            .OrderBy(p => p.Name)
            .Select(p => new ProductListItemDto(
                p.Id, p.Name, p.Sku, p.CategoryId, p.BrandId, p.BaseUnitId,
                p.CostPrice, p.SalePrice, p.TaxRate, p.IsActive, p.TrackStock))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ProductListItemDto>>(products);
    }
}
