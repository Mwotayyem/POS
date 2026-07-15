using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Catalog.Products.Common;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Catalog;
using SmartApp.Domain.Catalog.Enums;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Products.Commands.UpdateProduct;

/// <summary>
/// Updates a product's fields and fully replaces its child collections. Validates references and
/// child consistency, SKU uniqueness (excluding itself), and barcode uniqueness within the tenant
/// (excluding this product's own barcodes). The old child rows are removed and re-created to match
/// the request — simple and predictable for an edit form.
/// </summary>
public sealed class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public UpdateProductCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        Product? product = await _db.Products
            .Include(p => p.ProductUnits)
            .Include(p => p.Barcodes)
            .Include(p => p.Prices)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            return Result.Failure(Error.NotFound("المنتج غير موجود."));
        }

        Result validation = await ProductValidationHelper.ValidateAsync(
            _db, request.CategoryId, request.BrandId, request.BaseUnitId,
            request.Units, request.Barcodes, request.Prices, cancellationToken);
        if (validation.IsFailure)
        {
            return validation;
        }

        if (!string.IsNullOrWhiteSpace(request.Sku))
        {
            string sku = request.Sku.Trim();
            if (await _db.Products.AnyAsync(p => p.Id != product.Id && p.Sku == sku, cancellationToken))
            {
                return Result.Failure(Error.Conflict("رمز المنتج (SKU) مستخدم بالفعل."));
            }
        }

        Result barcodeUniqueness = await EnsureBarcodesAreFreeAsync(product.Id, request, cancellationToken);
        if (barcodeUniqueness.IsFailure)
        {
            return barcodeUniqueness;
        }

        // ---- Scalar fields ----
        product.Name = request.Name.Trim();
        product.Sku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim();
        product.CategoryId = request.CategoryId;
        product.BrandId = request.BrandId;
        product.BaseUnitId = request.BaseUnitId;
        product.CostPrice = request.CostPrice;
        product.SalePrice = request.SalePrice;
        product.TaxRate = request.TaxRate;
        product.ReorderLevel = request.ReorderLevel;
        product.IsActive = request.IsActive;
        product.TrackStock = request.TrackStock;
        product.CustomFieldsJson = string.IsNullOrWhiteSpace(request.CustomFieldsJson) ? null : request.CustomFieldsJson;

        // ---- Replace child collections ----
        _db.ProductUnits.RemoveRange(product.ProductUnits);
        _db.ProductBarcodes.RemoveRange(product.Barcodes);
        _db.ProductPrices.RemoveRange(product.Prices);

        foreach (var u in request.Units)
        {
            _db.ProductUnits.Add(new ProductUnit
            {
                ProductId = product.Id,
                UnitId = u.UnitId,
                ConversionFactor = u.ConversionFactor,
                Barcode = string.IsNullOrWhiteSpace(u.Barcode) ? null : u.Barcode.Trim(),
            });
        }

        foreach (var b in request.Barcodes)
        {
            _db.ProductBarcodes.Add(new ProductBarcode
            {
                ProductId = product.Id,
                Barcode = b.Barcode.Trim(),
                IsPrimary = b.IsPrimary,
            });
        }

        foreach (var pr in request.Prices)
        {
            _db.ProductPrices.Add(new ProductPrice
            {
                ProductId = product.Id,
                PriceType = (PriceType)pr.PriceType,
                Amount = pr.Amount,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Rejects barcodes already used by a DIFFERENT product in the tenant. The product's own current
    /// barcodes are excluded (they are being replaced).
    /// </summary>
    private async Task<Result> EnsureBarcodesAreFreeAsync(
        long productId, UpdateProductCommand request, CancellationToken cancellationToken)
    {
        if (request.Barcodes.Count == 0)
        {
            return Result.Success();
        }

        var values = request.Barcodes.Select(b => b.Barcode.Trim()).ToList();
        bool taken = await _db.ProductBarcodes
            .AnyAsync(b => b.ProductId != productId && values.Contains(b.Barcode), cancellationToken);
        return taken
            ? Result.Failure(Error.Conflict("باركود واحد أو أكثر مستخدم بالفعل."))
            : Result.Success();
    }
}
