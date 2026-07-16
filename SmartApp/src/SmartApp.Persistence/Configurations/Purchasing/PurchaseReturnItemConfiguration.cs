using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Purchasing;

namespace SmartApp.Persistence.Configurations.Purchasing;

/// <summary>
/// EF Core configuration for <see cref="PurchaseReturnItem"/>. Tenant-owned. Money DECIMAL(18,4).
/// Links to the original purchase invoice line (FK NO ACTION).
/// </summary>
public sealed class PurchaseReturnItemConfiguration : IEntityTypeConfiguration<PurchaseReturnItem>
{
    public void Configure(EntityTypeBuilder<PurchaseReturnItem> builder)
    {
        builder.ToTable("PurchaseReturnItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Quantity).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.UnitPrice).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.LineTotal).HasColumnType("decimal(18,4)").IsRequired();

        builder.Property(i => i.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(i => i.ConcurrencyStamp).IsRowVersion();

        builder.HasOne(i => i.PurchaseInvoiceItem)
            .WithMany()
            .HasForeignKey(i => i.PurchaseInvoiceItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.TenantId, i.PurchaseReturnId })
            .HasDatabaseName("IX_PurchaseReturnItems_Tenant_Return")
            .HasFilter("[IsDeleted] = 0");
    }
}
