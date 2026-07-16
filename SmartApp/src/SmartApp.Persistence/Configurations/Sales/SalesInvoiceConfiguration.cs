using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Sales;

namespace SmartApp.Persistence.Configurations.Sales;

/// <summary>
/// EF Core configuration for <see cref="SalesInvoice"/>. Tenant-owned. Money DECIMAL(18,4). Invoice
/// number unique per tenant. CustomerId nullable (cash sale). FKs NO ACTION. Mirrors
/// SmartApp-Architecture/06-Tables-Definitions.md §6.1.
/// </summary>
public sealed class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> builder)
    {
        builder.ToTable("SalesInvoices");
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

        builder.HasOne(i => i.Customer)
            .WithMany()
            .HasForeignKey(i => i.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Items)
            .WithOne(x => x.SalesInvoice!)
            .HasForeignKey(x => x.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.TenantId, i.InvoiceNumber })
            .IsUnique()
            .HasDatabaseName("UX_SalesInvoices_Tenant_Number")
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(i => new { i.TenantId, i.CustomerId })
            .HasDatabaseName("IX_SalesInvoices_Tenant_Customer")
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(i => new { i.TenantId, i.InvoiceDate })
            .HasDatabaseName("IX_SalesInvoices_Tenant_Date")
            .HasFilter("[IsDeleted] = 0");
    }
}
