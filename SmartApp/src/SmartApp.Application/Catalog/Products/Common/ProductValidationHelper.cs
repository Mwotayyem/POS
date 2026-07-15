using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Catalog.Products.Dtos;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Catalog.Enums;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Products.Common;

/// <summary>
/// Shared validation for product create/update: verifies the category / brand / base unit and any
/// child-unit references belong to the caller's tenant (the global filter hides other tenants'), and
/// that the child collections are internally consistent (unique barcodes, at most one primary barcode,
/// at most one price per type, known price types, positive conversion factors). Returns a
/// VALIDATION_ERROR describing the first problems found.
/// </summary>
internal static class ProductValidationHelper
{
    public static async Task<Result> ValidateAsync(
        IApplicationDbContext db,
        long? categoryId,
        long? brandId,
        long baseUnitId,
        IReadOnlyList<ProductUnitInput> units,
        IReadOnlyList<ProductBarcodeInput> barcodes,
        IReadOnlyList<ProductPriceInput> prices,
        CancellationToken cancellationToken)
    {
        var errors = new List<FieldError>();

        // ---- References (tenant-scoped) ----
        if (categoryId is long cid &&
            !await db.Categories.AnyAsync(c => c.Id == cid, cancellationToken))
        {
            errors.Add(new FieldError("categoryId", "التصنيف غير موجود."));
        }

        if (brandId is long bid &&
            !await db.Brands.AnyAsync(b => b.Id == bid, cancellationToken))
        {
            errors.Add(new FieldError("brandId", "العلامة التجارية غير موجودة."));
        }

        if (!await db.Units.AnyAsync(u => u.Id == baseUnitId, cancellationToken))
        {
            errors.Add(new FieldError("baseUnitId", "الوحدة الأساسية غير موجودة."));
        }

        // ---- Units ----
        if (units.Count > 0)
        {
            if (units.Any(u => u.ConversionFactor <= 0))
            {
                errors.Add(new FieldError("units", "معامل التحويل يجب أن يكون أكبر من صفر."));
            }

            var unitIds = units.Select(u => u.UnitId).ToList();
            if (unitIds.Distinct().Count() != unitIds.Count)
            {
                errors.Add(new FieldError("units", "لا يمكن تكرار نفس الوحدة للمنتج."));
            }

            List<long> distinctUnitIds = unitIds.Distinct().ToList();
            int foundUnits = await db.Units.CountAsync(u => distinctUnitIds.Contains(u.Id), cancellationToken);
            if (foundUnits != distinctUnitIds.Count)
            {
                errors.Add(new FieldError("units", "وحدة واحدة أو أكثر غير موجودة."));
            }
        }

        // ---- Barcodes ----
        if (barcodes.Count > 0)
        {
            var values = barcodes.Select(b => b.Barcode?.Trim()).ToList();
            if (values.Any(string.IsNullOrWhiteSpace))
            {
                errors.Add(new FieldError("barcodes", "الباركود لا يمكن أن يكون فارغاً."));
            }

            if (values.Where(v => !string.IsNullOrWhiteSpace(v))
                      .GroupBy(v => v).Any(g => g.Count() > 1))
            {
                errors.Add(new FieldError("barcodes", "لا يمكن تكرار الباركود لنفس المنتج."));
            }

            if (barcodes.Count(b => b.IsPrimary) > 1)
            {
                errors.Add(new FieldError("barcodes", "لا يمكن تعيين أكثر من باركود رئيسي واحد."));
            }
        }

        // ---- Prices ----
        if (prices.Count > 0)
        {
            if (prices.Any(p => p.Amount < 0))
            {
                errors.Add(new FieldError("prices", "السعر لا يمكن أن يكون سالباً."));
            }

            if (prices.Any(p => !Enum.IsDefined(typeof(PriceType), p.PriceType)))
            {
                errors.Add(new FieldError("prices", "نوع سعر غير معروف."));
            }

            var types = prices.Select(p => p.PriceType).ToList();
            if (types.Distinct().Count() != types.Count)
            {
                errors.Add(new FieldError("prices", "لا يمكن تكرار نفس نوع السعر."));
            }
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(Error.Validation("بيانات المنتج غير صالحة.", errors));
    }
}
