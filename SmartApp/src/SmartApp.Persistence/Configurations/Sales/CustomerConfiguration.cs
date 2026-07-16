using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Sales;

namespace SmartApp.Persistence.Configurations.Sales;

/// <summary>
/// EF Core configuration for <see cref="Customer"/>. Tenant-owned. Money DECIMAL(18,4). Name unique
/// per tenant. Mirrors SmartApp-Architecture/06-Tables-Definitions.md §5.1.
/// </summary>
public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Phone).HasMaxLength(30);
        builder.Property(c => c.Email).HasMaxLength(256);
        builder.Property(c => c.Address).HasMaxLength(400);
        builder.Property(c => c.CreditLimit).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(c => c.Balance).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(c => c.IsActive).HasDefaultValue(true);

        builder.Property(c => c.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(c => c.ConcurrencyStamp).IsRowVersion();

        builder.HasIndex(c => new { c.TenantId, c.Name })
            .IsUnique()
            .HasDatabaseName("UX_Customers_Tenant_Name")
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(c => new { c.TenantId, c.Phone })
            .HasDatabaseName("IX_Customers_Tenant_Phone")
            .HasFilter("[IsDeleted] = 0 AND [Phone] IS NOT NULL");
    }
}
