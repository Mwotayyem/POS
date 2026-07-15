using SmartApp.Domain.Common;
using SmartApp.Domain.Tenancy.Enums;

namespace SmartApp.Domain.Tenancy;

/// <summary>
/// A tenant (customer company/store). This is the definition of the tenant itself, so it does
/// NOT carry a <c>TenantId</c> and is NOT subject to the global tenant query filter — it is
/// managed by the system owner. It is soft-deletable and auditable.
/// See SmartApp-Architecture/05-Database-Design.md §6 and 06-Tables-Definitions.md §1.1.
///
/// <para>
/// <see cref="Status"/> is the complete replacement for any SaaS subscription model — the system
/// owner changes it manually. There are no plans, invoices, or payments.
/// </para>
/// </summary>
public sealed class Tenant : AuditableEntity, ISoftDeletable
{
    /// <summary>Externally exposed identifier (GUID) to avoid leaking the sequential Id (IDOR).</summary>
    public Guid PublicId { get; set; } = Guid.NewGuid();

    /// <summary>Company / store name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique tenant code (globally unique across the system).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Manual activation state controlled by the system owner.</summary>
    public TenantStatus Status { get; set; } = TenantStatus.Active;

    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    public DateTime? ActivatedDate { get; set; }
    public DateTime? SuspendedDate { get; set; }
    public DateTime? DisabledDate { get; set; }

    /// <summary>Free-text notes for the system owner.</summary>
    public string? Notes { get; set; }

    // ---- ISoftDeletable ----
    public bool IsDeleted { get; set; }
    public DateTime? DeletedDate { get; set; }
    public long? DeletedBy { get; set; }

    /// <summary>Optimistic concurrency token mapped to SQL Server <c>ROWVERSION</c>.</summary>
    public byte[] ConcurrencyStamp { get; set; } = Array.Empty<byte>();
}
