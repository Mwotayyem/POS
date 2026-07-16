using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Purchasing;

namespace SmartApp.Persistence.Configurations.Purchasing;

/// <summary>
/// EF Core configuration for <see cref="Supplier"/>. Tenant-owned. Money DECIMAL(18,4). Name unique
/// per tenant. Mirrors SmartApp-Architecture/06-Tables-Definitions.md §5.2.
/// </summary>
public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Phone).HasMaxLength(30);
        builder.Property(s => s.Email).HasMaxLength(256);
        builder.Property(s => s.Address).HasMaxLength(400);
        builder.Property(s => s.Balance).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(s => s.IsActive).HasDefaultValue(true);

        builder.Property(s => s.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(s => s.ConcurrencyStamp).IsRowVersion();

        builder.HasIndex(s => new { s.TenantId, s.Name })
            .IsUnique()
            .HasDatabaseName("UX_Suppliers_Tenant_Name")
            .HasFilter("[IsDeleted] = 0");
    }
}
