using SmartApp.Domain.Common;

namespace SmartApp.Domain.Sales;

/// <summary>
/// A customer the tenant sells to. Standalone table (not a shared Partner). Tenant-owned. Mirrors
/// SmartApp-Architecture/06-Tables-Definitions.md §5.1. <see cref="Balance"/> is the running
/// account-receivable balance: positive means the customer owes the tenant.
/// </summary>
public sealed class Customer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }

    /// <summary>Maximum outstanding balance allowed for this customer. DECIMAL(18,4).</summary>
    public decimal CreditLimit { get; set; }

    /// <summary>Running balance (A/R). Positive = the customer owes us. DECIMAL(18,4).</summary>
    public decimal Balance { get; set; }

    public bool IsActive { get; set; } = true;
}
