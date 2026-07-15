using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Catalog;

namespace SmartApp.Persistence.Configurations.Catalog;

/// <summary>
/// EF Core configuration for <see cref="Brand"/> (greenfield entity). Tenant-owned; follows the same
/// conventions as the documented Catalog tables. Name is unique per tenant.
/// </summary>
public sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name).HasMaxLength(200).IsRequired();
        builder.Property(b => b.Code).HasMaxLength(50);
        builder.Property(b => b.Description).HasMaxLength(500);
        builder.Property(b => b.IsActive).HasDefaultValue(true);

        builder.Property(b => b.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(b => b.ConcurrencyStamp).IsRowVersion();

        builder.HasIndex(b => new { b.TenantId, b.Name })
            .IsUnique()
            .HasDatabaseName("UX_Brands_Tenant_Name")
            .HasFilter("[IsDeleted] = 0");
    }
}
