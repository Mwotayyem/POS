using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Purchasing;

namespace SmartApp.Persistence.Configurations.Purchasing;

/// <summary>
/// EF Core configuration for <see cref="PurchaseReturn"/>. Tenant-owned. Return number unique per
/// tenant. FK to the original invoice, NO ACTION.
/// </summary>
public sealed class PurchaseReturnConfiguration : IEntityTypeConfiguration<PurchaseReturn>
{
    public void Configure(EntityTypeBuilder<PurchaseReturn> builder)
    {
        builder.ToTable("PurchaseReturns");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ReturnNumber).HasMaxLength(30).IsRequired();
        builder.Property(r => r.TotalAmount).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(r => r.Reason).HasMaxLength(300);

        builder.Property(r => r.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(r => r.ConcurrencyStamp).IsRowVersion();

        builder.HasOne(r => r.PurchaseInvoice)
            .WithMany()
            .HasForeignKey(r => r.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Items)
            .WithOne(x => x.PurchaseReturn!)
            .HasForeignKey(x => x.PurchaseReturnId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.TenantId, r.ReturnNumber })
            .IsUnique()
            .HasDatabaseName("UX_PurchaseReturns_Tenant_Number")
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(r => new { r.TenantId, r.PurchaseInvoiceId })
            .HasDatabaseName("IX_PurchaseReturns_Tenant_Invoice")
            .HasFilter("[IsDeleted] = 0");
    }
}
