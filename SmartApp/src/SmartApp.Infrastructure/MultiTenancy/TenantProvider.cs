using Microsoft.AspNetCore.Http;
using SmartApp.Application.Common.Interfaces;

namespace SmartApp.Infrastructure.MultiTenancy;

/// <summary>
/// Resolves the current tenant from the authenticated principal (the <c>tenant_id</c> claim),
/// never from client input. See SmartApp-Architecture/09-Multi-Tenant.md §3.
///
/// <para>
/// Phase 2 structure: the claim is populated once JWT authentication is added in Phase 3.
/// Until then <see cref="TenantIdOrNull"/> is <c>null</c>, which the global query filter treats
/// as an unrestricted (system) context — no tenant data exists yet, so nothing leaks.
/// </para>
/// </summary>
public sealed class TenantProvider : ITenantProvider
{
    private const string TenantClaimType = "tenant_id";
    private const string SystemOwnerClaimType = "is_system_owner";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsSystemOwner =>
        _httpContextAccessor.HttpContext?.User.FindFirst(SystemOwnerClaimType)?.Value == "true";

    public long? TenantIdOrNull
    {
        get
        {
            string? value = _httpContextAccessor.HttpContext?.User.FindFirst(TenantClaimType)?.Value;
            return long.TryParse(value, out long id) ? id : null;
        }
    }

    public bool IsResolved => IsSystemOwner || TenantIdOrNull is not null;

    public long CurrentTenantId =>
        TenantIdOrNull ?? throw new InvalidOperationException(
            "لم يُحلّ سياق المستأجر لهذا الطلب (Tenant not resolved).");
}
