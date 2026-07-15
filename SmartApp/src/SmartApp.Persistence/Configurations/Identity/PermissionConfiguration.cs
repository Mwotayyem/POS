using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Identity;

namespace SmartApp.Persistence.Configurations.Identity;

/// <summary>
/// EF Core configuration for <see cref="Permission"/> — a global reference table (no tenant id).
/// Permission code is globally unique. See SmartApp-Architecture/06-Tables-Definitions.md §2.3.
/// </summary>
public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");
        builder.HasKey(p => p.Id);

        // Int identity key (permissions are a small fixed catalog).
        builder.Property(p => p.Id).ValueGeneratedOnAdd();

        builder.Property(p => p.Code).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Module).HasMaxLength(50).IsRequired();
        builder.Property(p => p.DisplayName).HasMaxLength(200).IsRequired();

        builder.HasIndex(p => p.Code)
            .IsUnique()
            .HasDatabaseName("UX_Permissions_Code");
    }
}
