using SmartApp.Domain.Common;

namespace SmartApp.Domain.Catalog;

/// <summary>
/// A unit a product can be transacted in, with its conversion factor to the product's base unit
/// (e.g. 1 carton = 24 pieces). Tenant-owned. Mirrors SmartApp-Architecture/06-Tables-Definitions.md
/// §3.4. Carries an optional per-unit <see cref="Barcode"/>. <see cref="ConversionFactor"/> is
/// DECIMAL(18,6) and must be &gt; 0.
/// </summary>
public sealed class ProductUnit : BaseEntity
{
    public long ProductId { get; set; }
    public long UnitId { get; set; }

    /// <summary>Quantity of base units per one of this unit (e.g. 24). DECIMAL(18,6), &gt; 0.</summary>
    public decimal ConversionFactor { get; set; }

    /// <summary>Optional barcode specific to this product-unit packaging.</summary>
    public string? Barcode { get; set; }

    // ---- Navigations ----
    public Product? Product { get; set; }
    public Unit? Unit { get; set; }
}
