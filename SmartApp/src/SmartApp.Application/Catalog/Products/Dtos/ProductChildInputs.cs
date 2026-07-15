namespace SmartApp.Application.Catalog.Products.Dtos;

/// <summary>Input for a product-unit conversion row (create/update).</summary>
public sealed record ProductUnitInput(long UnitId, decimal ConversionFactor, string? Barcode);

/// <summary>Input for a product barcode (create/update).</summary>
public sealed record ProductBarcodeInput(string Barcode, bool IsPrimary);

/// <summary>Input for a typed price (create/update). PriceType is the numeric enum value.</summary>
public sealed record ProductPriceInput(byte PriceType, decimal Amount);
