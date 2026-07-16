using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Purchasing;

namespace SmartApp.Persistence.Configurations.Purchasing;

/// <summary>EF Core configuration for <see cref="PurchaseOrderItem"/>. Tenant-owned. Money DECIMAL(18,4).</summary>
public sealed class PurchaseOrderItemConfiguration : IEntityTypeConfiguration<PurchaseOrderItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        builder.ToTable("PurchaseOrderItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Quantity).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.UnitCost).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.LineTotal).HasColumnType("decimal(18,4)").IsRequired();

        builder.Property(i => i.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(i => i.ConcurrencyStamp).IsRowVersion();

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.TenantId, i.PurchaseOrderId })
            .HasDatabaseName("IX_PurchaseOrderItems_Tenant_Order")
            .HasFilter("[IsDeleted] = 0");
    }
}
