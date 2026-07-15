using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SmartApp.API.Authorization;
using SmartApp.Application.Catalog.Products.Commands.CreateProduct;
using SmartApp.Application.Catalog.Products.Commands.DeleteProduct;
using SmartApp.Application.Catalog.Products.Commands.UpdateProduct;
using SmartApp.Application.Catalog.Products.Dtos;
using SmartApp.Application.Catalog.Products.Queries.GetProductById;
using SmartApp.Application.Catalog.Products.Queries.GetProducts;
using SmartApp.Shared.Constants;

namespace SmartApp.API.Controllers.v1;

/// <summary>
/// Catalog products, including their unit conversions, barcodes (multiple, one primary), and typed
/// prices. CRUD guarded by product permissions and scoped to the caller's tenant.
/// See SmartApp-Architecture/06-Tables-Definitions.md §3.3–3.5.
/// </summary>
[ApiVersion("1.0")]
public sealed class ProductsController : ApiControllerBase
{
    /// <summary>Lists products with optional name/SKU search and category/brand filters.</summary>
    [HttpGet]
    [HasPermission(Permissions.Products.View)]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? search,
        [FromQuery] long? categoryId,
        [FromQuery] long? brandId,
        CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetProductsQuery(search, categoryId, brandId), cancellationToken));

    /// <summary>Gets a single product with its child collections.</summary>
    [HttpGet("{id:long}")]
    [HasPermission(Permissions.Products.View)]
    public async Task<IActionResult> GetProduct(long id, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetProductByIdQuery(id), cancellationToken));

    /// <summary>Creates a product (with optional units/barcodes/prices). Returns the new id.</summary>
    [HttpPost]
    [HasPermission(Permissions.Products.Create)]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new CreateProductCommand(
                request.Name, request.Sku, request.CategoryId, request.BrandId, request.BaseUnitId,
                request.CostPrice, request.SalePrice, request.TaxRate, request.ReorderLevel,
                request.TrackStock, request.CustomFieldsJson,
                request.Units ?? [], request.Barcodes ?? [], request.Prices ?? []),
            cancellationToken));

    /// <summary>Updates a product and replaces its child collections.</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Products.Update)]
    public async Task<IActionResult> UpdateProduct(
        long id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new UpdateProductCommand(
                id, request.Name, request.Sku, request.CategoryId, request.BrandId, request.BaseUnitId,
                request.CostPrice, request.SalePrice, request.TaxRate, request.ReorderLevel,
                request.IsActive, request.TrackStock, request.CustomFieldsJson,
                request.Units ?? [], request.Barcodes ?? [], request.Prices ?? []),
            cancellationToken));

    /// <summary>Deletes (soft) a product and its child rows.</summary>
    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Products.Delete)]
    public async Task<IActionResult> DeleteProduct(long id, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new DeleteProductCommand(id), cancellationToken));
}

/// <summary>Create-product request body.</summary>
public sealed record CreateProductRequest(
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
    IReadOnlyList<ProductUnitInput>? Units,
    IReadOnlyList<ProductBarcodeInput>? Barcodes,
    IReadOnlyList<ProductPriceInput>? Prices);

/// <summary>Update-product request body.</summary>
public sealed record UpdateProductRequest(
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
    IReadOnlyList<ProductUnitInput>? Units,
    IReadOnlyList<ProductBarcodeInput>? Barcodes,
    IReadOnlyList<ProductPriceInput>? Prices);
