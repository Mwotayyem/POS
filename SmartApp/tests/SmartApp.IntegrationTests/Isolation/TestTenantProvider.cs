using SmartApp.Application.Common.Interfaces;

namespace SmartApp.IntegrationTests.Isolation;

/// <summary>
/// Mutable tenant provider for tests — lets a test switch the "current tenant" to prove the
/// EF Core global query filter isolates data between tenants.
/// </summary>
public sealed class TestTenantProvider : ITenantProvider
{
    public long? TenantIdOrNull { get; set; }
    public bool IsSystemOwner { get; set; }

    public bool IsResolved => IsSystemOwner || TenantIdOrNull is not null;

    public long CurrentTenantId =>
        TenantIdOrNull ?? throw new InvalidOperationException("No tenant set in test provider.");

    public void SetTenant(long tenantId)
    {
        TenantIdOrNull = tenantId;
        IsSystemOwner = false;
    }
}
