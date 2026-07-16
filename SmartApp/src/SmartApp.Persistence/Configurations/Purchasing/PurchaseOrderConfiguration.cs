using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Purchasing;

namespace SmartApp.Persistence.Configurations.Purchasing;

/// <summary>
/// EF Core configuration for <see cref="PurchaseOrder"/> (greenfield). Tenant-owned. Order number
/// unique per tenant. FKs NO ACTION.
/// </summary>
public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderNumber).HasMaxLength(30).IsRequired();
        builder.Property(o => o.Status).HasConversion<byte>().IsRequired();
        builder.Property(o => o.Total).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(o => o.Notes).HasMaxLength(500);

        builder.Property(o => o.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(o => o.ConcurrencyStamp).IsRowVersion();

        builder.HasOne(o => o.Supplier)
            .WithMany()
            .HasForeignKey(o => o.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Items)
            .WithOne(i => i.PurchaseOrder!)
            .HasForeignKey(i => i.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => new { o.TenantId, o.OrderNumber })
            .IsUnique()
            .HasDatabaseName("UX_PurchaseOrders_Tenant_Number")
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(o => new { o.TenantId, o.SupplierId })
            .HasDatabaseName("IX_PurchaseOrders_Tenant_Supplier")
            .HasFilter("[IsDeleted] = 0");
    }
}
