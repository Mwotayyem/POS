using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Catalog.Products.Common;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Catalog;
using SmartApp.Domain.Catalog.Enums;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Products.Commands.CreateProduct;

/// <summary>
/// Creates a product with its child collections in one transaction. Validates references and child
/// consistency (see <see cref="ProductValidationHelper"/>), SKU uniqueness within the tenant, and
/// barcode uniqueness within the tenant. TenantId is auto-stamped on every BaseEntity row.
/// </summary>
public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<long>>
{
    private readonly IApplicationDbContext _db;

    public CreateProductCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        Result validation = await ProductValidationHelper.ValidateAsync(
            _db, request.CategoryId, request.BrandId, request.BaseUnitId,
            request.Units, request.Barcodes, request.Prices, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<long>(validation.Error!);
        }

        if (!string.IsNullOrWhiteSpace(request.Sku))
        {
            string sku = request.Sku.Trim();
            if (await _db.Products.AnyAsync(p => p.Sku == sku, cancellationToken))
            {
                return Result.Failure<long>(Error.Conflict("رمز المنتج (SKU) مستخدم بالفعل."));
            }
        }

        Result barcodeUniqueness = await EnsureBarcodesAreFreeAsync(request, cancellationToken);
        if (barcodeUniqueness.IsFailure)
        {
            return Result.Failure<long>(barcodeUniqueness.Error!);
        }

        var product = new Product
        {
            Name = request.Name.Trim(),
            Sku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim(),
            CategoryId = request.CategoryId,
            BrandId = request.BrandId,
            BaseUnitId = request.BaseUnitId,
            CostPrice = request.CostPrice,
            SalePrice = request.SalePrice,
            TaxRate = request.TaxRate,
            ReorderLevel = request.ReorderLevel,
            TrackStock = request.TrackStock,
            CustomFieldsJson = string.IsNullOrWhiteSpace(request.CustomFieldsJson) ? null : request.CustomFieldsJson,
            IsActive = true,
        };

        foreach (var u in request.Units)
        {
            product.ProductUnits.Add(new ProductUnit
            {
                UnitId = u.UnitId,
                ConversionFactor = u.ConversionFactor,
                Barcode = string.IsNullOrWhiteSpace(u.Barcode) ? null : u.Barcode.Trim(),
            });
        }

        foreach (var b in request.Barcodes)
        {
            product.Barcodes.Add(new ProductBarcode
            {
                Barcode = b.Barcode.Trim(),
                IsPrimary = b.IsPrimary,
            });
        }

        foreach (var pr in request.Prices)
        {
            product.Prices.Add(new ProductPrice
            {
                PriceType = (PriceType)pr.PriceType,
                Amount = pr.Amount,
            });
        }

        _db.Products.Add(product);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(product.Id);
    }

    /// <summary>Rejects barcodes already taken by another product in the tenant (unique per tenant).</summary>
    private async Task<Result> EnsureBarcodesAreFreeAsync(
        CreateProductCommand request, CancellationToken cancellationToken)
    {
        if (request.Barcodes.Count == 0)
        {
            return Result.Success();
        }

        var values = request.Barcodes.Select(b => b.Barcode.Trim()).ToList();
        bool taken = await _db.ProductBarcodes.AnyAsync(b => values.Contains(b.Barcode), cancellationToken);
        return taken
            ? Result.Failure(Error.Conflict("باركود واحد أو أكثر مستخدم بالفعل."))
            : Result.Success();
    }
}
