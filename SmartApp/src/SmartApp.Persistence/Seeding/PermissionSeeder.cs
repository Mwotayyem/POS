using Microsoft.EntityFrameworkCore;
using SmartApp.Domain.Identity;
using SmartApp.Persistence.Context;
using SmartApp.Shared.Constants;

namespace SmartApp.Persistence.Seeding;

/// <summary>
/// Seeds the global (tenant-independent) Permission reference table from the fixed
/// <see cref="PermissionCatalog"/>. Idempotent: inserts only codes that are missing, so it is safe
/// to run on every startup. Permissions are never created at runtime — this is their source of truth.
/// See SmartApp-Architecture/10-Identity-RBAC.md §2.
/// </summary>
public static class PermissionSeeder
{
    /// <summary>
    /// Ensures every permission code in the catalog exists in the database. Returns the number of
    /// permissions inserted. Permission is a global reference table (no tenant), so no tenant context
    /// is required.
    /// </summary>
    public static async Task<int> SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        // Permission has no tenant filter, but IgnoreQueryFilters keeps this robust regardless.
        HashSet<string> existing = (await db.Permissions
                .IgnoreQueryFilters()
                .Select(p => p.Code)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var toAdd = PermissionCatalog.All
            .Where(def => !existing.Contains(def.Code))
            .Select(def => new Permission
            {
                Code = def.Code,
                Module = def.Module,
                DisplayName = def.DisplayName,
            })
            .ToList();

        if (toAdd.Count == 0)
        {
            return 0;
        }

        db.Permissions.AddRange(toAdd);
        await db.SaveChangesAsync(cancellationToken);
        return toAdd.Count;
    }
}
