using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Catalog;

namespace SmartApp.Persistence.Configurations.Catalog;

/// <summary>
/// EF Core configuration for <see cref="Unit"/> (unit of measure). Tenant-owned. Mirrors
/// SmartApp-Architecture/06-Tables-Definitions.md §3.2 (+ Precision). Name is unique per tenant.
/// </summary>
public sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("Units");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Name).HasMaxLength(50).IsRequired();
        builder.Property(u => u.Symbol).HasMaxLength(20);
        builder.Property(u => u.IsActive).HasDefaultValue(true);

        builder.Property(u => u.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(u => u.ConcurrencyStamp).IsRowVersion();

        // Unit name unique within a tenant, excluding soft-deleted.
        builder.HasIndex(u => new { u.TenantId, u.Name })
            .IsUnique()
            .HasDatabaseName("UX_Units_Tenant_Name")
            .HasFilter("[IsDeleted] = 0");
    }
}
