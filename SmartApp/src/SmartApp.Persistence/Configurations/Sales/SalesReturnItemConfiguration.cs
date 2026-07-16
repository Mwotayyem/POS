using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Sales;

namespace SmartApp.Persistence.Configurations.Sales;

/// <summary>
/// EF Core configuration for <see cref="SalesReturnItem"/>. Tenant-owned. Money DECIMAL(18,4). Links
/// to the original sales invoice line (FK NO ACTION).
/// </summary>
public sealed class SalesReturnItemConfiguration : IEntityTypeConfiguration<SalesReturnItem>
{
    public void Configure(EntityTypeBuilder<SalesReturnItem> builder)
    {
        builder.ToTable("SalesReturnItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Quantity).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.UnitPrice).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.UnitCost).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(i => i.LineTotal).HasColumnType("decimal(18,4)").IsRequired();

        builder.Property(i => i.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(i => i.ConcurrencyStamp).IsRowVersion();

        builder.HasOne(i => i.SalesInvoiceItem)
            .WithMany()
            .HasForeignKey(i => i.SalesInvoiceItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.TenantId, i.SalesReturnId })
            .HasDatabaseName("IX_SalesReturnItems_Tenant_Return")
            .HasFilter("[IsDeleted] = 0");
    }
}
