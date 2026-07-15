using MediatR;
using SmartApp.Application.Catalog.Products.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Products.Commands.UpdateProduct;

/// <summary>
/// Updates a product and replaces its child collections (units, barcodes, prices) with the provided
/// sets. See SmartApp-Architecture/06-Tables-Definitions.md §3.3–3.5.
/// </summary>
public sealed record UpdateProductCommand(
    long ProductId,
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
    string? CustomFieldsJson,
    IReadOnlyList<ProductUnitInput> Units,
    IReadOnlyList<ProductBarcodeInput> Barcodes,
    IReadOnlyList<ProductPriceInput> Prices) : IRequest<Result>;
