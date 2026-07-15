using Microsoft.EntityFrameworkCore;
using SmartApp.Domain.Catalog;
using SmartApp.Domain.Identity;
using SmartApp.Domain.Tenancy;

namespace SmartApp.Application.Common.Interfaces;

/// <summary>
/// Application-facing abstraction over the EF Core database context. The Application layer
/// depends on this interface, not on the concrete AppDbContext in the Persistence layer
/// (Dependency Inversion). See SmartApp-Architecture/02-Solution-Architecture.md §3.
///
/// <para>DbSets are added here as each entity is introduced.</para>
/// </summary>
public interface IApplicationDbContext
{
    // ---- Tenancy ----
    DbSet<Tenant> Tenants { get; }
    DbSet<TenantSetting> TenantSettings { get; }

    // ---- Identity ----
    DbSet<AppUser> Users { get; }
    DbSet<AppRole> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    // ---- Catalog ----
    DbSet<Category> Categories { get; }
    DbSet<Unit> Units { get; }
    DbSet<Brand> Brands { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductUnit> ProductUnits { get; }
    DbSet<ProductBarcode> ProductBarcodes { get; }
    DbSet<ProductPrice> ProductPrices { get; }

    /// <summary>
    /// Persists pending changes. Tenant stamping, soft-delete conversion, and audit-field
    /// population are applied by the context/interceptors before the write reaches the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
