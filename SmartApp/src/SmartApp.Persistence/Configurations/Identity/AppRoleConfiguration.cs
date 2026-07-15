using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Identity;

namespace SmartApp.Persistence.Configurations.Identity;

/// <summary>
/// EF Core configuration for <see cref="AppRole"/> (tenant-owned; globally filtered via BaseEntity).
/// Role name is unique within a tenant. See SmartApp-Architecture/06-Tables-Definitions.md §2.2.
/// </summary>
public sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.NormalizedName).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(300);

        builder.Property(r => r.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(r => r.ConcurrencyStamp).IsRowVersion();

        // Role name unique within a tenant, excluding soft-deleted.
        builder.HasIndex(r => new { r.TenantId, r.NormalizedName })
            .IsUnique()
            .HasDatabaseName("UX_Roles_Tenant_Name")
            .HasFilter("[IsDeleted] = 0");

        builder.HasMany(r => r.RolePermissions)
            .WithOne(rp => rp.Role!)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.UserRoles)
            .WithOne(ur => ur.Role!)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
