using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Catalog;

namespace SmartApp.Persistence.Configurations.Catalog;

/// <summary>
/// EF Core configuration for <see cref="ProductPrice"/> (greenfield typed prices). Tenant-owned;
/// Amount DECIMAL(18,4) &gt;= 0. At most one price per (product, type) within a tenant.
/// </summary>
public sealed class ProductPriceConfiguration : IEntityTypeConfiguration<ProductPrice>
{
    public void Configure(EntityTypeBuilder<ProductPrice> builder)
    {
        builder.ToTable("ProductPrices", t =>
            t.HasCheckConstraint("CK_ProductPrices_Amount", "[Amount] >= 0"));
        builder.HasKey(pp => pp.Id);

        builder.Property(pp => pp.PriceType).HasConversion<byte>().IsRequired();
        builder.Property(pp => pp.Amount).HasColumnType("decimal(18,4)").IsRequired();

        builder.Property(pp => pp.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(pp => pp.ConcurrencyStamp).IsRowVersion();

        // One price per (product, type) within a tenant, excluding soft-deleted.
        builder.HasIndex(pp => new { pp.TenantId, pp.ProductId, pp.PriceType })
            .IsUnique()
            .HasDatabaseName("UX_ProductPrices_Tenant_Product_Type")
            .HasFilter("[IsDeleted] = 0");
    }
}
