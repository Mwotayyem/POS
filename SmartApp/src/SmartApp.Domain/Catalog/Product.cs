using SmartApp.Domain.Common;

namespace SmartApp.Domain.Catalog;

/// <summary>
/// A catalog product — the hub of the Catalog module. Tenant-owned. Mirrors
/// SmartApp-Architecture/06-Tables-Definitions.md §3.3 (Sku, Category, BaseUnit, CostPrice, SalePrice,
/// TaxRate, ReorderLevel, IsActive, TrackStock, CustomFieldsJson), plus an optional
/// <see cref="BrandId"/> (Brand is a greenfield addition, not in the docs).
///
/// <para>Pricing is inline per the governing spec: <see cref="CostPrice"/> is the current weighted
/// average cost, <see cref="SalePrice"/> the default selling price. Optional tiered pricing lives in
/// <see cref="ProductPrice"/>. A product with stock movements is deactivated, not deleted
/// (07-ERD-Relationships.md §4).</para>
/// </summary>
public sealed class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Internal stock-keeping unit code. Unique per tenant when present.</summary>
    public string? Sku { get; set; }

    public long? CategoryId { get; set; }

    /// <summary>Optional brand (greenfield; not in the governing schema).</summary>
    public long? BrandId { get; set; }

    /// <summary>The product's base unit of measure (required).</summary>
    public long BaseUnitId { get; set; }

    /// <summary>Current weighted average cost (WAC). DECIMAL(18,4), &gt;= 0.</summary>
    public decimal CostPrice { get; set; }

    /// <summary>Default selling price. DECIMAL(18,4), &gt;= 0.</summary>
    public decimal SalePrice { get; set; }

    /// <summary>Product tax rate as a percentage. DECIMAL(9,4).</summary>
    public decimal TaxRate { get; set; }

    /// <summary>Low-stock threshold used by reorder reporting. DECIMAL(18,4).</summary>
    public decimal ReorderLevel { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Whether inventory is tracked for this product.</summary>
    public bool TrackStock { get; set; } = true;

    /// <summary>Optional per-tenant custom fields as JSON (validated at the DB via ISJSON).</summary>
    public string? CustomFieldsJson { get; set; }

    // ---- Navigations ----
    public Category? Category { get; set; }
    public Brand? Brand { get; set; }
    public Unit? BaseUnit { get; set; }
    public ICollection<ProductUnit> ProductUnits { get; set; } = new List<ProductUnit>();
    public ICollection<ProductBarcode> Barcodes { get; set; } = new List<ProductBarcode>();
    public ICollection<ProductPrice> Prices { get; set; } = new List<ProductPrice>();
}
