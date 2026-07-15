using SmartApp.Domain.Common;

namespace SmartApp.Domain.Catalog;

/// <summary>
/// A unit of measure (piece, carton, kg, ...). Tenant-owned. Mirrors
/// SmartApp-Architecture/06-Tables-Definitions.md §3.2 (Name, Symbol), plus a <see cref="Precision"/>
/// (number of decimal places allowed when expressing a quantity in this unit — e.g. 0 for pieces,
/// 3 for kilograms).
/// </summary>
public sealed class Unit : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Symbol { get; set; }

    /// <summary>Decimal places allowed for quantities in this unit (0–6).</summary>
    public byte Precision { get; set; }

    public bool IsActive { get; set; } = true;

    // ---- Navigations ----
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<ProductUnit> ProductUnits { get; set; } = new List<ProductUnit>();
}
