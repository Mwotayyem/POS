using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Sales;

namespace SmartApp.Persistence.Configurations.Sales;

/// <summary>
/// EF Core configuration for <see cref="SalesInvoiceItem"/>. Tenant-owned. Money DECIMAL(18,4), tax
/// rate DECIMAL(9,4). Keeps both UnitPrice and UnitCost snapshots. ReturnedQty guarded
/// 0 ≤ ReturnedQty ≤ Quantity (over-return prevention).
/// </summary>
public sealed class SalesInvoiceItemConfiguration : IEntityTypeConfiguration<SalesInvoiceItem>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceItem> builder)
    {
        builder.ToTable("SalesInvoiceItems", t =>
            t.HasCheckConstraint("CK_SalesInvoiceItems_Returned",
                "[ReturnedQty] >= 0 AND [ReturnedQty] <= [Quantity]"));
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Quantity).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.UnitPrice).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.UnitCost).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
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

        builder.HasIndex(i => new { i.TenantId, i.SalesInvoiceId })
            .HasDatabaseName("IX_SalesInvoiceItems_Tenant_Invoice")
            .HasFilter("[IsDeleted] = 0");
    }
}
