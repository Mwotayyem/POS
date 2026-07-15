using SmartApp.Domain.Common;

namespace SmartApp.Domain.Catalog;

/// <summary>
/// A product brand / manufacturer. Not defined in the architecture docs — designed here following the
/// same tenant-owned <see cref="BaseEntity"/> conventions (soft-delete, ROWVERSION, NO ACTION FKs).
/// Products optionally reference a brand.
/// </summary>
public sealed class Brand : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional short code.</summary>
    public string? Code { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    // ---- Navigations ----
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
