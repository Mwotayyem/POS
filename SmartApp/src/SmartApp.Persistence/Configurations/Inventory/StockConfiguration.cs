using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Inventory;

namespace SmartApp.Persistence.Configurations.Inventory;

/// <summary>
/// EF Core configuration for <see cref="Stock"/> (balance per product+warehouse). Tenant-owned.
/// Money DECIMAL(18,4). One row per (product, warehouse) within a tenant.
/// </summary>
public sealed class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("Stocks", t =>
            t.HasCheckConstraint("CK_Stocks_AvgCost", "[AvgCost] >= 0"));
        builder.HasKey(s => s.Id);

        builder.Property(s => s.QtyOnHand).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(s => s.AvgCost).HasColumnType("decimal(18,4)").HasDefaultValue(0m);

        builder.Property(s => s.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(s => s.ConcurrencyStamp).IsRowVersion();

        builder.HasOne(s => s.Product)
            .WithMany()
            .HasForeignKey(s => s.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Warehouse)
            .WithMany(w => w.Stocks)
            .HasForeignKey(s => s.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        // One balance row per (product, warehouse) within a tenant.
        builder.HasIndex(s => new { s.TenantId, s.ProductId, s.WarehouseId })
            .IsUnique()
            .HasDatabaseName("UX_Stocks_Tenant_Product_Warehouse")
            .HasFilter("[IsDeleted] = 0");
    }
}
