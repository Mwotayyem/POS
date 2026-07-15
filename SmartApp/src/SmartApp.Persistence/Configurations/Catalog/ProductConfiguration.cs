using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Catalog;

namespace SmartApp.Persistence.Configurations.Catalog;

/// <summary>
/// EF Core configuration for <see cref="Product"/>. Tenant-owned. Mirrors
/// SmartApp-Architecture/06-Tables-Definitions.md §3.3 (money DECIMAL(18,4), SKU unique per tenant,
/// NO ACTION FKs, price check), plus the greenfield Brand FK. The ISJSON check on CustomFieldsJson is
/// applied provider-guarded in AppDbContext (SQL Server only).
/// </summary>
public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", t =>
        {
            // Numeric guards (supported on both SQL Server and SQLite).
            t.HasCheckConstraint("CK_Products_Prices", "[CostPrice] >= 0 AND [SalePrice] >= 0");
        });
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(250).IsRequired();
        builder.Property(p => p.Sku).HasMaxLength(60);

        builder.Property(p => p.CostPrice).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(p => p.SalePrice).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(p => p.TaxRate).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
        builder.Property(p => p.ReorderLevel).HasColumnType("decimal(18,4)").HasDefaultValue(0m);

        builder.Property(p => p.IsActive).HasDefaultValue(true);
        builder.Property(p => p.TrackStock).HasDefaultValue(true);

        builder.Property(p => p.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(p => p.ConcurrencyStamp).IsRowVersion();

        // FKs — all ON DELETE NO ACTION (soft-delete; dependency checks in the Application layer).
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Brand)
            .WithMany(b => b.Products)
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.BaseUnit)
            .WithMany(u => u.Products)
            .HasForeignKey(p => p.BaseUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.ProductUnits)
            .WithOne(pu => pu.Product!)
            .HasForeignKey(pu => pu.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Barcodes)
            .WithOne(b => b.Product!)
            .HasForeignKey(b => b.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Prices)
            .WithOne(pr => pr.Product!)
            .HasForeignKey(pr => pr.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // SKU unique per tenant (only when present), excluding soft-deleted.
        builder.HasIndex(p => new { p.TenantId, p.Sku })
            .IsUnique()
            .HasDatabaseName("UX_Products_Tenant_Sku")
            .HasFilter("[IsDeleted] = 0 AND [Sku] IS NOT NULL");

        builder.HasIndex(p => new { p.TenantId, p.Name })
            .HasDatabaseName("IX_Products_Tenant_Name")
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(p => new { p.TenantId, p.CategoryId })
            .HasDatabaseName("IX_Products_Tenant_Category")
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(p => new { p.TenantId, p.BrandId })
            .HasDatabaseName("IX_Products_Tenant_Brand")
            .HasFilter("[IsDeleted] = 0");
    }
}
