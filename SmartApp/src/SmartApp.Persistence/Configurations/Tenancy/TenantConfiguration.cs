using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Tenancy;

namespace SmartApp.Persistence.Configurations.Tenancy;

/// <summary>
/// EF Core configuration for <see cref="Tenant"/>. Mirrors the schema in
/// SmartApp-Architecture/06-Tables-Definitions.md §1.1. The tenant table is not tenant-owned,
/// so it has no TenantId column and no global query filter.
/// </summary>
public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.PublicId)
            .IsRequired();

        builder.Property(t => t.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Code)
            .HasMaxLength(50)
            .IsRequired();

        // Persist the enum as its numeric (TINYINT) value — stable across renames.
        builder.Property(t => t.Status)
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(t => t.ContactEmail).HasMaxLength(256);
        builder.Property(t => t.ContactPhone).HasMaxLength(30);
        builder.Property(t => t.Notes).HasMaxLength(1000);

        builder.Property(t => t.CreatedDate)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        // Optimistic concurrency via SQL Server ROWVERSION.
        builder.Property(t => t.ConcurrencyStamp)
            .IsRowVersion();

        // Unique tenant code (global). Filtered to exclude soft-deleted rows.
        builder.HasIndex(t => t.Code)
            .IsUnique()
            .HasDatabaseName("UX_Tenants_Code")
            .HasFilter("[IsDeleted] = 0");

        // Unique external id.
        builder.HasIndex(t => t.PublicId)
            .IsUnique()
            .HasDatabaseName("UX_Tenants_PublicId");

        // Lookup by status (system-owner tenant management).
        builder.HasIndex(t => t.Status)
            .HasDatabaseName("IX_Tenants_Status")
            .HasFilter("[IsDeleted] = 0");

        // Status domain guard: 1=Active, 2=Suspended, 3=Disabled.
        builder.ToTable(t => t.HasCheckConstraint("CK_Tenants_Status", "[Status] IN (1,2,3)"));
    }
}
