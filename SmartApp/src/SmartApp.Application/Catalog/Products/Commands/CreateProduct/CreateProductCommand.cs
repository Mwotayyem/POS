using MediatR;
using SmartApp.Application.Catalog.Products.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Products.Commands.CreateProduct;

/// <summary>
/// Creates a product in the current tenant, together with its optional child collections (unit
/// conversions, barcodes, typed prices). Returns the new product id. TenantId is stamped server-side.
/// See SmartApp-Architecture/06-Tables-Definitions.md §3.3–3.5.
/// </summary>
public sealed record CreateProductCommand(
    string Name,
    string? Sku,
    long? CategoryId,
    long? BrandId,
    long BaseUnitId,
    decimal CostPrice,
    decimal SalePrice,
    decimal TaxRate,
    decimal ReorderLevel,
    bool TrackStock,
    string? CustomFieldsJson,
    IReadOnlyList<ProductUnitInput> Units,
    IReadOnlyList<ProductBarcodeInput> Barcodes,
    IReadOnlyList<ProductPriceInput> Prices) : IRequest<Result<long>>;
