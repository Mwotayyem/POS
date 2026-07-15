namespace SmartApp.Application.Common.Interfaces;

/// <summary>
/// Single source of truth for the current tenant within a request. The tenant is resolved
/// server-side from the authenticated principal (JWT claim) — NEVER from client input.
/// Implemented in the Infrastructure layer. See SmartApp-Architecture/09-Multi-Tenant.md §3.
/// </summary>
public interface ITenantProvider
{
    /// <summary>
    /// The current tenant id. Throws if a tenant is required but not resolved.
    /// </summary>
    long CurrentTenantId { get; }

    /// <summary>True when a tenant (or system-owner context) has been resolved for this request.</summary>
    bool IsResolved { get; }

    /// <summary>
    /// True for system-owner requests, which operate above tenants (e.g. managing tenants).
    /// System-owner writes are not auto-stamped with a TenantId.
    /// </summary>
    bool IsSystemOwner { get; }

    /// <summary>
    /// The resolved tenant id or <c>null</c> when unresolved / system-owner — safe to read
    /// without throwing (used by the query filter and stamping logic).
    /// </summary>
    long? TenantIdOrNull { get; }
}
