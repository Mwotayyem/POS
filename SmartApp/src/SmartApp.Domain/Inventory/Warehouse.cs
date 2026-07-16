using SmartApp.Domain.Common;

namespace SmartApp.Domain.Inventory;

/// <summary>
/// A stock-holding location. Tenant-owned (inherits <see cref="BaseEntity"/>). Stock balances and
/// movements reference a warehouse. See SmartApp-Architecture/06-Tables-Definitions.md (Inventory).
/// </summary>
public sealed class Warehouse : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional short code (unique per tenant when present).</summary>
    public string? Code { get; set; }

    public string? Address { get; set; }

    /// <summary>Whether this is the tenant's default warehouse for new operations.</summary>
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    // ---- Navigations ----
    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
}
