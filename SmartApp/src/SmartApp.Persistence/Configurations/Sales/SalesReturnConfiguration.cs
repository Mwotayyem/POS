using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Sales;

namespace SmartApp.Persistence.Configurations.Sales;

/// <summary>
/// EF Core configuration for <see cref="SalesReturn"/>. Tenant-owned. Return number unique per
/// tenant. FK to the original invoice, NO ACTION.
/// </summary>
public sealed class SalesReturnConfiguration : IEntityTypeConfiguration<SalesReturn>
{
    public void Configure(EntityTypeBuilder<SalesReturn> builder)
    {
        builder.ToTable("SalesReturns");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ReturnNumber).HasMaxLength(30).IsRequired();
        builder.Property(r => r.TotalAmount).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(r => r.Reason).HasMaxLength(300);

        builder.Property(r => r.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(r => r.ConcurrencyStamp).IsRowVersion();

        builder.HasOne(r => r.SalesInvoice)
            .WithMany()
            .HasForeignKey(r => r.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Items)
            .WithOne(x => x.SalesReturn!)
            .HasForeignKey(x => x.SalesReturnId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.TenantId, r.ReturnNumber })
            .IsUnique()
            .HasDatabaseName("UX_SalesReturns_Tenant_Number")
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(r => new { r.TenantId, r.SalesInvoiceId })
            .HasDatabaseName("IX_SalesReturns_Tenant_Invoice")
            .HasFilter("[IsDeleted] = 0");
    }
}
