using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Catalog;

namespace SmartApp.Persistence.Configurations.Catalog;

/// <summary>
/// EF Core configuration for <see cref="ProductBarcode"/>. Tenant-owned. Mirrors
/// SmartApp-Architecture/06-Tables-Definitions.md §3.5 — barcode value is unique per tenant (the
/// primary POS lookup). At most one primary barcode per product (enforced in the Application layer).
/// </summary>
public sealed class ProductBarcodeConfiguration : IEntityTypeConfiguration<ProductBarcode>
{
    public void Configure(EntityTypeBuilder<ProductBarcode> builder)
    {
        builder.ToTable("ProductBarcodes");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Barcode).HasMaxLength(60).IsRequired();
        builder.Property(b => b.IsPrimary).HasDefaultValue(false);

        builder.Property(b => b.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(b => b.ConcurrencyStamp).IsRowVersion();

        // Barcode unique within a tenant (most important POS search key), excluding soft-deleted.
        builder.HasIndex(b => new { b.TenantId, b.Barcode })
            .IsUnique()
            .HasDatabaseName("UX_ProductBarcodes_Tenant_Barcode")
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(b => new { b.TenantId, b.ProductId })
            .HasDatabaseName("IX_ProductBarcodes_Tenant_Product")
            .HasFilter("[IsDeleted] = 0");
    }
}
