using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Purchasing;

namespace SmartApp.Persistence.Configurations.Purchasing;

/// <summary>
/// EF Core configuration for <see cref="PurchaseInvoice"/>. Tenant-owned. Money DECIMAL(18,4).
/// Invoice number unique per tenant. FKs NO ACTION. Mirrors SalesInvoices with SupplierId
/// (06-Tables-Definitions.md §6.1 + §7 deltas).
/// </summary>
public sealed class PurchaseInvoiceConfiguration : IEntityTypeConfiguration<PurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoice> builder)
    {
        builder.ToTable("PurchaseInvoices");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.InvoiceNumber).HasMaxLength(30).IsRequired();
        builder.Property(i => i.Status).HasConversion<byte>().IsRequired();
        builder.Property(i => i.SubTotal).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(i => i.DiscountTotal).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(i => i.TaxTotal).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(i => i.GrandTotal).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(i => i.PaidAmount).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(i => i.Notes).HasMaxLength(500);

        builder.Property(i => i.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(i => i.ConcurrencyStamp).IsRowVersion();

        builder.HasOne(i => i.Supplier)
            .WithMany()
            .HasForeignKey(i => i.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Items)
            .WithOne(x => x.PurchaseInvoice!)
            .HasForeignKey(x => x.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.TenantId, i.InvoiceNumber })
            .IsUnique()
            .HasDatabaseName("UX_PurchaseInvoices_Tenant_Number")
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(i => new { i.TenantId, i.SupplierId })
            .HasDatabaseName("IX_PurchaseInvoices_Tenant_Supplier")
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(i => new { i.TenantId, i.InvoiceDate })
            .HasDatabaseName("IX_PurchaseInvoices_Tenant_Date")
            .HasFilter("[IsDeleted] = 0");
    }
}
