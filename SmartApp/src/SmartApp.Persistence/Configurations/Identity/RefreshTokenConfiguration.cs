using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Identity;

namespace SmartApp.Persistence.Configurations.Identity;

/// <summary>
/// EF Core configuration for <see cref="RefreshToken"/>. The token is stored only as a SHA-256 hash.
/// Tenant-scoped (nullable, mirrors the user); its tenant filter is applied in AppDbContext.
/// See SmartApp-Architecture/06-Tables-Definitions.md §2.6.
/// </summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(rt => rt.ReplacedByTokenHash).HasMaxLength(64);
        builder.Property(rt => rt.CreatedByIp).HasMaxLength(45);
        builder.Property(rt => rt.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        // Fast lookup by token hash (validation + rotation).
        builder.HasIndex(rt => rt.TokenHash).HasDatabaseName("IX_RefreshTokens_TokenHash");

        // Cleanup of expired/active tokens.
        builder.HasIndex(rt => rt.ExpiresAt).HasDatabaseName("IX_RefreshTokens_ExpiresAt");
    }
}
