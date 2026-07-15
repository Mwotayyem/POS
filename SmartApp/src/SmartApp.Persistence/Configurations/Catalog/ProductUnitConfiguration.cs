using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Catalog;

namespace SmartApp.Persistence.Configurations.Catalog;

/// <summary>
/// EF Core configuration for <see cref="ProductUnit"/> (unit conversions). Tenant-owned. Mirrors
/// SmartApp-Architecture/06-Tables-Definitions.md §3.4 (ConversionFactor DECIMAL(18,6) &gt; 0). A unit
/// appears at most once per product (filtered unique index).
/// </summary>
public sealed class ProductUnitConfiguration : IEntityTypeConfiguration<ProductUnit>
{
    public void Configure(EntityTypeBuilder<ProductUnit> builder)
    {
        builder.ToTable("ProductUnits", t =>
            t.HasCheckConstraint("CK_ProductUnits_Factor", "[ConversionFactor] > 0"));
        builder.HasKey(pu => pu.Id);

        builder.Property(pu => pu.ConversionFactor).HasColumnType("decimal(18,6)").IsRequired();
        builder.Property(pu => pu.Barcode).HasMaxLength(60);

        builder.Property(pu => pu.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(pu => pu.ConcurrencyStamp).IsRowVersion();

        builder.HasOne(pu => pu.Unit)
            .WithMany(u => u.ProductUnits)
            .HasForeignKey(pu => pu.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        // One row per (product, unit) within a tenant, excluding soft-deleted.
        builder.HasIndex(pu => new { pu.TenantId, pu.ProductId, pu.UnitId })
            .IsUnique()
            .HasDatabaseName("UX_ProductUnits_Tenant_Product_Unit")
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(pu => new { pu.TenantId, pu.ProductId })
            .HasDatabaseName("IX_ProductUnits_Tenant_Product")
            .HasFilter("[IsDeleted] = 0");
    }
}
