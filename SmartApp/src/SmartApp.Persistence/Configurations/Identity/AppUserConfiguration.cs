using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Identity;

namespace SmartApp.Persistence.Configurations.Identity;

/// <summary>
/// EF Core configuration for <see cref="AppUser"/>. TenantId is nullable (system owner), so the
/// tenant query filter is applied in AppDbContext, not here. Username/email uniqueness is scoped to
/// the tenant, not global. See SmartApp-Architecture/06-Tables-Definitions.md §2.1.
/// </summary>
public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.NormalizedEmail).HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Phone).HasMaxLength(30);

        builder.Property(u => u.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(u => u.ConcurrencyStamp).IsRowVersion();

        // Email unique within a tenant (two tenants may reuse the same email), excluding soft-deleted.
        builder.HasIndex(u => new { u.TenantId, u.NormalizedEmail })
            .IsUnique()
            .HasDatabaseName("UX_Users_Tenant_Email")
            .HasFilter("[IsDeleted] = 0");

        builder.HasMany(u => u.UserRoles)
            .WithOne(ur => ur.User!)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.RefreshTokens)
            .WithOne(rt => rt.User!)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
