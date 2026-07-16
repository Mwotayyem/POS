using SmartApp.Domain.Common;

namespace SmartApp.Domain.Purchasing;

/// <summary>
/// A supplier the tenant purchases from. Standalone table (not a shared Partner). Tenant-owned.
/// Mirrors SmartApp-Architecture/06-Tables-Definitions.md §5.2. <see cref="Balance"/> is the running
/// account-payable balance: positive means the tenant owes the supplier.
/// </summary>
public sealed class Supplier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }

    /// <summary>Running balance (A/P). Positive = we owe the supplier. DECIMAL(18,4).</summary>
    public decimal Balance { get; set; }

    public bool IsActive { get; set; } = true;
}
