using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Tenancy;
using SmartApp.Domain.Tenancy.Enums;
using SmartApp.Persistence.Context;

namespace SmartApp.API.Middleware;

/// <summary>
/// Resolves the current tenant from the authenticated principal and enforces the manual
/// activation gate: a tenant whose <see cref="TenantStatus"/> is not <c>Active</c> is rejected
/// with 403 before any business logic runs. See SmartApp-Architecture/09-Multi-Tenant.md §4–5.
///
/// <para>
/// Phase 3 keeps this deliberately simple:
/// </para>
/// <list type="bullet">
///   <item><description>System-owner requests pass through (they operate above tenants).</description></item>
///   <item><description>Requests without a resolved tenant (e.g. Swagger, unauthenticated) pass through — the JWT that carries the tenant claim is issued in a later phase.</description></item>
///   <item><description>When a tenant IS resolved, its status is checked directly against the database. Caching is a later optimization (see §4).</description></item>
/// </list>
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantProvider tenantProvider,
        AppDbContext dbContext)
    {
        // System owner operates above tenants — no per-tenant status gate.
        if (tenantProvider.IsSystemOwner)
        {
            await _next(context);
            return;
        }

        long? tenantId = tenantProvider.TenantIdOrNull;

        // No tenant resolved for this request (unauthenticated / public) — nothing to gate yet.
        if (tenantId is null)
        {
            await _next(context);
            return;
        }

        // A tenant is resolved — enforce the manual activation gate.
        // IgnoreQueryFilters: Tenants is not tenant-filtered, but be explicit that this lookup
        // is a control-plane check independent of any business filter.
        Tenant? tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId.Value && !t.IsDeleted, context.RequestAborted);

        if (tenant is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (tenant.Status != TenantStatus.Active)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                data = (object?)null,
                error = new { code = "TENANT_INACTIVE", message = "الحساب غير مُفعَّل. راجع مزوّد الخدمة." },
                meta = new { correlationId = context.TraceIdentifier, timestamp = DateTime.UtcNow.ToString("O") },
            }, context.RequestAborted);
            return;
        }

        await _next(context);
    }
}

/// <summary>Extension to register the tenant resolution middleware.</summary>
public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}
