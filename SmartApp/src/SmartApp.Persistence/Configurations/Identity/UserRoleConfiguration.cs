using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Identity;

namespace SmartApp.Persistence.Configurations.Identity;

/// <summary>
/// EF Core configuration for the <see cref="UserRole"/> join table. Composite key (UserId, RoleId).
/// Tenant-scoped; its tenant query filter is applied in AppDbContext.
/// See SmartApp-Architecture/06-Tables-Definitions.md §2.5.
/// </summary>
public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");
        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.Property(ur => ur.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(ur => ur.TenantId).HasDatabaseName("IX_UserRoles_Tenant");
    }
}
