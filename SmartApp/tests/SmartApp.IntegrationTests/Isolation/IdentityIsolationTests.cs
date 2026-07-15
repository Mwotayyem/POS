using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartApp.Domain.Identity;
using SmartApp.Domain.Tenancy;
using SmartApp.Persistence.Context;
using Xunit;

namespace SmartApp.IntegrationTests.Isolation;

/// <summary>
/// Proves multi-tenant isolation across the Identity entities: users, roles, role-permission grants,
/// and refresh tokens are all scoped to their tenant. See SmartApp-Architecture/10-Identity-RBAC.md
/// and 09-Multi-Tenant.md §8.
/// </summary>
public sealed class IdentityIsolationTests : IDisposable
{
    private const long TenantA = 1001;
    private const long TenantB = 1002;

    private readonly TenantDbContextFactory _factory = new();

    public IdentityIsolationTests()
    {
        _factory.TenantProvider.IsSystemOwner = true;
        using AppDbContext ctx = _factory.Create();
        ctx.Tenants.AddRange(
            new Tenant { Id = TenantA, Name = "Tenant A", Code = "A" },
            new Tenant { Id = TenantB, Name = "Tenant B", Code = "B" });
        ctx.SaveChanges();
        _factory.TenantProvider.IsSystemOwner = false;
    }

    [Fact]
    public void User_Is_Not_Visible_Across_Tenants()
    {
        // Tenant B creates a user.
        _factory.TenantProvider.SetTenant(TenantB);
        using (AppDbContext ctxB = _factory.Create())
        {
            ctxB.Users.Add(new AppUser
            {
                TenantId = TenantB,
                Email = "b@b.com",
                NormalizedEmail = "B@B.COM",
                PasswordHash = "x",
                FullName = "User B",
            });
            ctxB.SaveChanges();
        }

        // Tenant A must not see it.
        _factory.TenantProvider.SetTenant(TenantA);
        using AppDbContext ctxA = _factory.Create();
        ctxA.Users.Any(u => u.Email == "b@b.com").Should().BeFalse();
        ctxA.Users.Should().BeEmpty();
    }

    [Fact]
    public void Role_And_Permission_Grants_Are_Isolated_Per_Tenant()
    {
        // A shared, global permission (reference data — no tenant).
        _factory.TenantProvider.IsSystemOwner = true;
        int permissionId;
        using (AppDbContext ctx = _factory.Create())
        {
            var permission = new Permission { Code = "products.view", Module = "products", DisplayName = "View" };
            ctx.Permissions.Add(permission);
            ctx.SaveChanges();
            permissionId = permission.Id;
        }
        _factory.TenantProvider.IsSystemOwner = false;

        // Tenant B creates a role and grants it the permission.
        _factory.TenantProvider.SetTenant(TenantB);
        using (AppDbContext ctxB = _factory.Create())
        {
            var role = new AppRole { TenantId = TenantB, Name = "Manager", NormalizedName = "MANAGER" };
            ctxB.Roles.Add(role);
            ctxB.SaveChanges();

            ctxB.RolePermissions.Add(new RolePermission
            {
                TenantId = TenantB,
                RoleId = role.Id,
                PermissionId = permissionId,
            });
            ctxB.SaveChanges();
        }

        // Tenant A sees neither B's role nor B's grant...
        _factory.TenantProvider.SetTenant(TenantA);
        using (AppDbContext ctxA = _factory.Create())
        {
            ctxA.Roles.Should().BeEmpty();
            ctxA.RolePermissions.Should().BeEmpty();

            // ...but the global Permission catalog is shared (reference data).
            ctxA.Permissions.Any(p => p.Code == "products.view").Should().BeTrue();
        }

        // Tenant B still sees its own role and grant.
        _factory.TenantProvider.SetTenant(TenantB);
        using (AppDbContext ctxB = _factory.Create())
        {
            ctxB.Roles.Should().ContainSingle(r => r.Name == "Manager");
            ctxB.RolePermissions.Should().ContainSingle(rp => rp.PermissionId == permissionId);
        }
    }

    [Fact]
    public void RefreshToken_Is_Owned_By_Its_Tenant_Only()
    {
        // Tenant B: a user with a refresh token.
        _factory.TenantProvider.SetTenant(TenantB);
        byte[] tokenHash = [1, 2, 3, 4];
        using (AppDbContext ctxB = _factory.Create())
        {
            var user = new AppUser
            {
                TenantId = TenantB,
                Email = "owner@b.com",
                NormalizedEmail = "OWNER@B.COM",
                PasswordHash = "x",
                FullName = "Owner B",
            };
            ctxB.Users.Add(user);
            ctxB.SaveChanges();

            ctxB.RefreshTokens.Add(new RefreshToken
            {
                TenantId = TenantB,
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            ctxB.SaveChanges();
        }

        // Tenant A cannot see Tenant B's refresh token (no cross-tenant token theft).
        _factory.TenantProvider.SetTenant(TenantA);
        using (AppDbContext ctxA = _factory.Create())
        {
            ctxA.RefreshTokens.Any(rt => rt.TokenHash == tokenHash).Should().BeFalse();
            ctxA.RefreshTokens.Should().BeEmpty();
        }

        // Tenant B still owns it.
        _factory.TenantProvider.SetTenant(TenantB);
        using (AppDbContext ctxB = _factory.Create())
        {
            ctxB.RefreshTokens.Should().ContainSingle();
        }
    }

    public void Dispose() => _factory.Dispose();
}
