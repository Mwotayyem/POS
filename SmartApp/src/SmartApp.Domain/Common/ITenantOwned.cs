namespace SmartApp.Domain.Common;

/// <summary>
/// Marks an entity as owned by a tenant. The <see cref="TenantId"/> is the isolation
/// boundary — it is stamped server-side and NEVER accepted from the client.
/// Every entity implementing this participates in the EF Core global tenant query filter.
/// See SmartApp-Architecture/09-Multi-Tenant.md.
/// </summary>
public interface ITenantOwned
{
    long TenantId { get; set; }
}
