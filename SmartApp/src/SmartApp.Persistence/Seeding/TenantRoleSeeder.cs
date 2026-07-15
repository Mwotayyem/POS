using Microsoft.EntityFrameworkCore;
using SmartApp.Domain.Identity;
using SmartApp.Persistence.Context;
using SmartApp.Shared.Constants;

namespace SmartApp.Persistence.Seeding;

/// <summary>
/// Provisions a tenant's default <c>Owner</c> role and grants it every permission in the catalog.
/// Called when a tenant is created (and reused by tests). Idempotent per tenant: if the Owner role
/// already exists, only missing permission grants are added.
///
/// <para>
/// Runs cross-tenant (IgnoreQueryFilters) and stamps TenantId explicitly, because provisioning may
/// happen in a system-owner context where the ambient tenant filter would otherwise hide the rows.
/// See SmartApp-Architecture/10-Identity-RBAC.md §3.
/// </para>
/// </summary>
public static class TenantRoleSeeder
{
    /// <summary>
    /// Ensures the given tenant has an Owner role granted all catalog permissions. Returns the
    /// Owner role id. Assumes the global Permission catalog has already been seeded
    /// (see <see cref="PermissionSeeder"/>).
    /// </summary>
    public static async Task<long> SeedOwnerRoleAsync(
        AppDbContext db, long tenantId, CancellationToken cancellationToken = default)
    {
        AppRole? owner = await db.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                r => r.TenantId == tenantId && r.NormalizedName == NormalizedOwner && !r.IsDeleted,
                cancellationToken);

        if (owner is null)
        {
            owner = new AppRole
            {
                TenantId = tenantId,
                Name = RoleNames.Owner,
                NormalizedName = NormalizedOwner,
                Description = "المالك — يملك جميع الصلاحيات داخل المستأجر.",
                IsSystemRole = true,
            };
            db.Roles.Add(owner);
            await db.SaveChangesAsync(cancellationToken);
        }

        await GrantAllPermissionsAsync(db, tenantId, owner.Id, cancellationToken);
        return owner.Id;
    }

    private static async Task GrantAllPermissionsAsync(
        AppDbContext db, long tenantId, long roleId, CancellationToken cancellationToken)
    {
        List<Permission> permissions = await db.Permissions
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        HashSet<int> alreadyGranted = (await db.RolePermissions
                .IgnoreQueryFilters()
                .Where(rp => rp.RoleId == roleId)
                .Select(rp => rp.PermissionId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var toAdd = permissions
            .Where(p => !alreadyGranted.Contains(p.Id))
            .Select(p => new RolePermission
            {
                RoleId = roleId,
                PermissionId = p.Id,
                TenantId = tenantId,
            })
            .ToList();

        if (toAdd.Count == 0)
        {
            return;
        }

        db.RolePermissions.AddRange(toAdd);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static readonly string NormalizedOwner = RoleNames.Owner.ToUpperInvariant();
}
