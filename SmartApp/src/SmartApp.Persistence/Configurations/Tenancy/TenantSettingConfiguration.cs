using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Tenancy;

namespace SmartApp.Persistence.Configurations.Tenancy;

/// <summary>
/// EF Core configuration for <see cref="TenantSetting"/> (tenant-owned; one row per tenant).
/// Mirrors SmartApp-Architecture/06-Tables-Definitions.md §1.2. The global tenant query filter
/// is applied automatically by AppDbContext because this inherits BaseEntity.
/// </summary>
public sealed class TenantSettingConfiguration : IEntityTypeConfiguration<TenantSetting>
{
    public void Configure(EntityTypeBuilder<TenantSetting> builder)
    {
        builder.ToTable("TenantSettings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Currency).HasMaxLength(3).IsRequired().HasDefaultValue("SAR");
        builder.Property(s => s.TimeZone).HasMaxLength(60).IsRequired().HasDefaultValue("UTC");
        builder.Property(s => s.Locale).HasMaxLength(10).IsRequired().HasDefaultValue("ar");
        builder.Property(s => s.DefaultTaxRate).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
        builder.Property(s => s.ThemeJson);

        builder.Property(s => s.CreatedDate)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(s => s.ConcurrencyStamp)
            .IsRowVersion();

        // One settings row per tenant (filtered to exclude soft-deleted).
        builder.HasIndex(s => s.TenantId)
            .IsUnique()
            .HasDatabaseName("UX_TenantSettings_Tenant")
            .HasFilter("[IsDeleted] = 0");

        // NOTE: the "ThemeJson must be valid JSON" check constraint (ISJSON) is SQL Server-specific
        // and is applied in AppDbContext.OnModelCreating, guarded by provider, so SQLite-backed tests
        // are not broken by an unsupported function.
    }
}
