using SmartApp.Domain.Common;

namespace SmartApp.Domain.Sequences;

/// <summary>
/// A per-tenant running counter for a <see cref="DocumentType"/>, used to generate sequential
/// document numbers (e.g. PINV-000123). One row per (tenant, document type). Tenant-owned.
/// Atomic increment happens in the numbering service inside the document's transaction, so no two
/// documents share a number. See SmartApp-Architecture/05-Database-Design.md §10.
///
/// <para>Not soft-deletable in practice (counters are permanent), but it inherits <see cref="BaseEntity"/>
/// for the tenant filter + concurrency stamp, which also guards concurrent increments.</para>
/// </summary>
public sealed class DocumentSequence : BaseEntity
{
    public DocumentType DocumentType { get; set; }

    /// <summary>Optional prefix for the formatted number (e.g. "PINV-").</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>The last number issued. The next document gets <c>LastNumber + 1</c>.</summary>
    public long LastNumber { get; set; }
}
