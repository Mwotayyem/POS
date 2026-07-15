using SmartApp.Domain.Common;

namespace SmartApp.Domain.Catalog;

/// <summary>
/// A barcode for a product. A product may have many barcodes; each barcode value is unique within the
/// tenant. Tenant-owned. Mirrors SmartApp-Architecture/06-Tables-Definitions.md §3.5, plus an
/// <see cref="IsPrimary"/> flag marking the product's primary barcode (at most one per product,
/// enforced in the Application layer). This is the primary POS lookup key.
/// </summary>
public sealed class ProductBarcode : BaseEntity
{
    public long ProductId { get; set; }

    public string Barcode { get; set; } = string.Empty;

    /// <summary>True for the product's primary barcode (at most one per product).</summary>
    public bool IsPrimary { get; set; }

    // ---- Navigations ----
    public Product? Product { get; set; }
}
