using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Sales;

namespace SmartApp.Persistence.Configurations.Sales;

/// <summary>
/// EF Core configuration for <see cref="Payment"/> (customer debt settlement). Tenant-owned. Money
/// DECIMAL(18,4). Payment number unique per tenant. FKs NO ACTION. Mirrors
/// SmartApp-Architecture/06-Tables-Definitions.md §5.3.
/// </summary>
public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.PaymentNumber).HasMaxLength(30).IsRequired();
        builder.Property(p => p.Amount).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(p => p.Method).HasConversion<byte>().IsRequired();
        builder.Property(p => p.Reference).HasMaxLength(100);
        builder.Property(p => p.Notes).HasMaxLength(300);

        builder.Property(p => p.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(p => p.ConcurrencyStamp).IsRowVersion();

        builder.HasOne(p => p.Customer)
            .WithMany()
            .HasForeignKey(p => p.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.SalesInvoice)
            .WithMany()
            .HasForeignKey(p => p.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.TenantId, p.PaymentNumber })
            .IsUnique()
            .HasDatabaseName("UX_Payments_Tenant_Number")
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(p => new { p.TenantId, p.CustomerId })
            .HasDatabaseName("IX_Payments_Tenant_Customer")
            .HasFilter("[IsDeleted] = 0");
    }
}
