using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Purchasing;

namespace SmartApp.Persistence.Configurations.Purchasing;

/// <summary>
/// EF Core configuration for <see cref="PurchaseInvoiceItem"/>. Tenant-owned. Money DECIMAL(18,4),
/// tax rate DECIMAL(9,4). ReturnedQty is guarded 0 ≤ ReturnedQty ≤ Quantity (over-return prevention,
/// 13-Development-Rules.md §6.1).
/// </summary>
public sealed class PurchaseInvoiceItemConfiguration : IEntityTypeConfiguration<PurchaseInvoiceItem>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoiceItem> builder)
    {
        builder.ToTable("PurchaseInvoiceItems", t =>
            t.HasCheckConstraint("CK_PurchaseInvoiceItems_Returned",
                "[ReturnedQty] >= 0 AND [ReturnedQty] <= [Quantity]"));
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Quantity).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.UnitPrice).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.DiscountAmount).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(i => i.TaxRate).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
        builder.Property(i => i.TaxAmount).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(i => i.LineTotal).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.ReturnedQty).HasColumnType("decimal(18,4)").HasDefaultValue(0m);

        builder.Property(i => i.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(i => i.ConcurrencyStamp).IsRowVersion();

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.TenantId, i.PurchaseInvoiceId })
            .HasDatabaseName("IX_PurchaseInvoiceItems_Tenant_Invoice")
            .HasFilter("[IsDeleted] = 0");
    }
}
