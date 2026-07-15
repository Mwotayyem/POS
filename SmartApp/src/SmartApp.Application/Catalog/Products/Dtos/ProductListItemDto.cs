namespace SmartApp.Application.Catalog.Products.Dtos;

/// <summary>A lightweight product row for list views (no child collections).</summary>
public sealed record ProductListItemDto(
    long Id,
    string Name,
    string? Sku,
    long? CategoryId,
    long? BrandId,
    long BaseUnitId,
    decimal CostPrice,
    decimal SalePrice,
    decimal TaxRate,
    bool IsActive,
    bool TrackStock);
