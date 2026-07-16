using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Inventory;

namespace SmartApp.Persistence.Configurations.Inventory;

/// <summary>
/// EF Core configuration for <see cref="Warehouse"/>. Tenant-owned (global filter in AppDbContext).
/// Name unique per tenant; optional code unique per tenant when present.
/// </summary>
public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name).HasMaxLength(200).IsRequired();
        builder.Property(w => w.Code).HasMaxLength(50);
        builder.Property(w => w.Address).HasMaxLength(500);
        builder.Property(w => w.IsDefault).HasDefaultValue(false);
        builder.Property(w => w.IsActive).HasDefaultValue(true);

        builder.Property(w => w.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(w => w.ConcurrencyStamp).IsRowVersion();

        builder.HasIndex(w => new { w.TenantId, w.Name })
            .IsUnique()
            .HasDatabaseName("UX_Warehouses_Tenant_Name")
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(w => new { w.TenantId, w.Code })
            .IsUnique()
            .HasDatabaseName("UX_Warehouses_Tenant_Code")
            .HasFilter("[IsDeleted] = 0 AND [Code] IS NOT NULL");
    }
}
