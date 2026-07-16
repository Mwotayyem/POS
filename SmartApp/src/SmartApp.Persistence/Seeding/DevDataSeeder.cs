using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Identity;
using SmartApp.Domain.Tenancy;
using SmartApp.Domain.Tenancy.Enums;
using SmartApp.Persistence.Context;

namespace SmartApp.Persistence.Seeding;

/// <summary>
/// Development-only convenience seeder that provisions a first, ready-to-use login so the SPA can be
/// exercised immediately after the database is created. It creates (once) an <b>Active</b> tenant,
/// its <c>Owner</c> role granted every permission (via <see cref="TenantRoleSeeder"/>), and an active
/// Owner user with a known password.
///
/// <para>
/// Idempotent and safe: it is a no-op if <b>any</b> user already exists, so it never overwrites real
/// data and never runs twice. It assumes the global permission catalog has already been seeded
/// (see <see cref="PermissionSeeder"/>). It is intended to be gated to the Development environment by
/// the composition root — it must never run in production.
/// </para>
/// </summary>
public static class DevDataSeeder
{
    /// <summary>Outcome of a dev-seed attempt (for logging).</summary>
    public enum SeedOutcome
    {
        /// <summary>An Owner user was created.</summary>
        Created,

        /// <summary>Skipped because at least one user already exists.</summary>
        SkippedUsersExist,
    }

    /// <summary>
    /// Ensures a default Owner login exists. Returns <see cref="SeedOutcome.Created"/> when a new user
    /// was inserted, or <see cref="SeedOutcome.SkippedUsersExist"/> when the database already has users.
    /// </summary>
    public static async Task<SeedOutcome> SeedAsync(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        string ownerEmail,
        string ownerPassword,
        string tenantName,
        string tenantCode,
        CancellationToken cancellationToken = default)
    {
        // No-op if the system is already provisioned with any user (across all tenants).
        bool anyUser = await db.Users.IgnoreQueryFilters().AnyAsync(cancellationToken);
        if (anyUser)
        {
            return SeedOutcome.SkippedUsersExist;
        }

        // 1) An active tenant (reuse an existing one with the same code if present).
        Tenant? tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Code == tenantCode && !t.IsDeleted, cancellationToken);

        if (tenant is null)
        {
            tenant = new Tenant
            {
                Name = tenantName,
                Code = tenantCode,
                Status = TenantStatus.Active,
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(cancellationToken);
        }

        // 2) Owner role granted all permissions.
        long ownerRoleId = await TenantRoleSeeder.SeedOwnerRoleAsync(db, tenant.Id, cancellationToken);

        // 3) The Owner user.
        var user = new AppUser
        {
            TenantId = tenant.Id,
            Email = ownerEmail,
            NormalizedEmail = ownerEmail.Trim().ToUpperInvariant(),
            PasswordHash = passwordHasher.Hash(ownerPassword),
            FullName = "System Owner",
            IsActive = true,
            EmailConfirmed = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        // 4) Assign the Owner role to the user.
        db.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = ownerRoleId,
            TenantId = tenant.Id,
        });
        await db.SaveChangesAsync(cancellationToken);

        return SeedOutcome.Created;
    }
}
