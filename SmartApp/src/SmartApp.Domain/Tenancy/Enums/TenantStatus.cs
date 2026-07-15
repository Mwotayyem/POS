namespace SmartApp.Domain.Tenancy.Enums;

/// <summary>
/// Manual activation state of a tenant, controlled by the system owner.
/// This is the full replacement for any SaaS subscription/billing model —
/// there are no plans, invoices, or payments. See SmartApp-Architecture/09-Multi-Tenant.md §5.
/// Numeric values are stable and persisted (TINYINT) — do not reorder.
/// </summary>
public enum TenantStatus : byte
{
    /// <summary>Fully operational — login and all operations allowed.</summary>
    Active = 1,

    /// <summary>Temporarily halted — login and API access rejected (403). Data retained. Reversible.</summary>
    Suspended = 2,

    /// <summary>Permanently disabled — same rejection as Suspended, intended as a final state. Kept for archival.</summary>
    Disabled = 3,
}
