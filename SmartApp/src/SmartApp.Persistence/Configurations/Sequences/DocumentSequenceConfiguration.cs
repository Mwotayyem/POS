using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartApp.Domain.Sequences;

namespace SmartApp.Persistence.Configurations.Sequences;

/// <summary>
/// EF Core configuration for <see cref="DocumentSequence"/> (per-tenant document counters).
/// Tenant-owned. One row per (tenant, document type). See 05-Database-Design.md §10.
/// </summary>
public sealed class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> builder)
    {
        builder.ToTable("DocumentSequences");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.DocumentType).HasConversion<byte>().IsRequired();
        builder.Property(s => s.Prefix).HasMaxLength(20).IsRequired();
        builder.Property(s => s.LastNumber).IsRequired();

        builder.Property(s => s.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(s => s.ConcurrencyStamp).IsRowVersion();

        builder.HasIndex(s => new { s.TenantId, s.DocumentType })
            .IsUnique()
            .HasDatabaseName("UX_DocumentSequences_Tenant_Type")
            .HasFilter("[IsDeleted] = 0");
    }
}
