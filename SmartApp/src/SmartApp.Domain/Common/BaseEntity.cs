namespace SmartApp.Domain.Common;

/// <summary>
/// Base class for every tenant-owned business entity. Carries the mandatory shared
/// columns defined in SmartApp-Architecture/05-Database-Design.md §2:
/// Id, TenantId, audit fields, soft-delete fields, and an optimistic-concurrency stamp.
///
/// <para>
/// Rules enforced by the persistence layer (Phase 2 structure, wired in later phases):
/// </para>
/// <list type="bullet">
///   <item><description><see cref="ITenantOwned.TenantId"/> is stamped server-side; a global query filter scopes every read to the current tenant.</description></item>
///   <item><description>Deletes become soft deletes (<see cref="ISoftDeletable.IsDeleted"/>).</description></item>
///   <item><description>Audit fields are populated automatically.</description></item>
///   <item><description><see cref="ConcurrencyStamp"/> maps to a SQL Server <c>ROWVERSION</c>.</description></item>
/// </list>
/// </summary>
public abstract class BaseEntity : AuditableEntity, ITenantOwned, ISoftDeletable
{
    /// <summary>Isolation boundary. Stamped server-side — never accepted from the client.</summary>
    public long TenantId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedDate { get; set; }
    public long? DeletedBy { get; set; }

    /// <summary>Optimistic concurrency token mapped to SQL Server <c>ROWVERSION</c>.</summary>
    public byte[] ConcurrencyStamp { get; set; } = Array.Empty<byte>();
}
