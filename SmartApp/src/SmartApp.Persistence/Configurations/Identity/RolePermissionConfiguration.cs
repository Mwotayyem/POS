using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Identity;

namespace SmartApp.Persistence.Configurations.Identity;

/// <summary>
/// EF Core configuration for the <see cref="RolePermission"/> join table. Composite key
/// (RoleId, PermissionId). Tenant-scoped; its tenant query filter is applied in AppDbContext.
/// See SmartApp-Architecture/06-Tables-Definitions.md §2.4.
/// </summary>
public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });

        builder.Property(rp => rp.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(rp => rp.Permission!)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(rp => rp.TenantId).HasDatabaseName("IX_RolePermissions_Tenant");
    }
}
