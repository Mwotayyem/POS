using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Catalog.Products.Dtos;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Products.Queries.GetProductById;

/// <summary>
/// Loads one tenant product with its units, barcodes, and typed prices projected. All child
/// collections are tenant-filtered, so nothing from another tenant can appear.
/// </summary>
public sealed class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
{
    private readonly IApplicationDbContext _db;

    public GetProductByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        ProductDto? product = await _db.Products
            .Where(p => p.Id == request.ProductId)
            .Select(p => new ProductDto(
                p.Id, p.Name, p.Sku, p.CategoryId, p.BrandId, p.BaseUnitId,
                p.CostPrice, p.SalePrice, p.TaxRate, p.ReorderLevel, p.IsActive, p.TrackStock,
                p.ProductUnits
                    .OrderBy(u => u.Id)
                    .Select(u => new ProductUnitDto(u.Id, u.UnitId, u.ConversionFactor, u.Barcode))
                    .ToList(),
                p.Barcodes
                    .OrderByDescending(b => b.IsPrimary).ThenBy(b => b.Id)
                    .Select(b => new ProductBarcodeDto(b.Id, b.Barcode, b.IsPrimary))
                    .ToList(),
                p.Prices
                    .OrderBy(pr => pr.PriceType)
                    .Select(pr => new ProductPriceDto(pr.Id, (byte)pr.PriceType, pr.Amount))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return product is null
            ? Result.Failure<ProductDto>(Error.NotFound("المنتج غير موجود."))
            : Result.Success(product);
    }
}
