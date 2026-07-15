namespace SmartApp.Application.Catalog.Products.Dtos;

/// <summary>
/// A product with its child collections (units, barcodes, typed prices). Returned by get-by-id.
/// See SmartApp-Architecture/06-Tables-Definitions.md §3.3–3.5.
/// </summary>
public sealed record ProductDto(
    long Id,
    string Name,
    string? Sku,
    long? CategoryId,
    long? BrandId,
    long BaseUnitId,
    decimal CostPrice,
    decimal SalePrice,
    decimal TaxRate,
    decimal ReorderLevel,
    bool IsActive,
    bool TrackStock,
    IReadOnlyList<ProductUnitDto> Units,
    IReadOnlyList<ProductBarcodeDto> Barcodes,
    IReadOnlyList<ProductPriceDto> Prices);

/// <summary>A product-unit conversion row.</summary>
public sealed record ProductUnitDto(long Id, long UnitId, decimal ConversionFactor, string? Barcode);

/// <summary>A product barcode row.</summary>
public sealed record ProductBarcodeDto(long Id, string Barcode, bool IsPrimary);

/// <summary>A typed price row.</summary>
public sealed record ProductPriceDto(long Id, byte PriceType, decimal Amount);
