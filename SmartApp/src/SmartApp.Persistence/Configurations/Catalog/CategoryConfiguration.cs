using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Catalog;

namespace SmartApp.Persistence.Configurations.Catalog;

/// <summary>
/// EF Core configuration for <see cref="Category"/>. Tenant-owned (global filter applied in
/// AppDbContext). Self-referencing tree via ParentId. Mirrors
/// SmartApp-Architecture/06-Tables-Definitions.md §3.1 (+ SortOrder).
/// </summary>
public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Code).HasMaxLength(50);
        builder.Property(c => c.IsActive).HasDefaultValue(true);

        builder.Property(c => c.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(c => c.ConcurrencyStamp).IsRowVersion();

        // Self-FK (tree). NO ACTION — deletes are soft; cycle prevention is in the Application layer.
        builder.HasOne(c => c.Parent)
            .WithMany(c => c.Children)
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.TenantId, c.ParentId })
            .HasDatabaseName("IX_Categories_Tenant_Parent")
            .HasFilter("[IsDeleted] = 0");
    }
}
