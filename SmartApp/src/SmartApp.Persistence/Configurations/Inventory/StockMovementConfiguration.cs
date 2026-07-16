using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Inventory;

namespace SmartApp.Persistence.Configurations.Inventory;

/// <summary>
/// EF Core configuration for <see cref="StockMovement"/> — the append-only ledger. It is NOT a
/// BaseEntity: no soft-delete, no ROWVERSION (append-only rows are never updated/deleted; the
/// interceptor enforces this). Only creation audit is kept. Money DECIMAL(18,4). The tenant query
/// filter is applied explicitly in AppDbContext (like the Identity nullable-tenant entities).
/// Mirrors SmartApp-Architecture/06-Tables-Definitions.md §4.2.
/// </summary>
public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.MovementType).HasConversion<byte>().IsRequired();
        builder.Property(m => m.QuantityChange).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(m => m.UnitCost).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(m => m.ResultingQty).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(m => m.ResultingAvgCost).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(m => m.Reason).HasMaxLength(300);
        builder.Property(m => m.ReferenceCode).HasMaxLength(50);

        builder.Property(m => m.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(m => m.Product)
            .WithMany()
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Warehouse)
            .WithMany()
            .HasForeignKey(m => m.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Item movement statement: by product over time.
        builder.HasIndex(m => new { m.TenantId, m.ProductId, m.OccurredAt })
            .HasDatabaseName("IX_StockMovements_Tenant_Product_Date");

        // Transfer legs / source-document tracing.
        builder.HasIndex(m => new { m.TenantId, m.ReferenceCode })
            .HasDatabaseName("IX_StockMovements_Tenant_Reference");
    }
}
