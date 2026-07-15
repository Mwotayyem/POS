using SmartApp.Domain.Common;

namespace SmartApp.Domain.Catalog;

/// <summary>
/// A product category. Categories form a tree via <see cref="ParentId"/> (self-reference).
/// Tenant-owned (inherits <see cref="BaseEntity"/>) and globally tenant-filtered.
/// Mirrors SmartApp-Architecture/06-Tables-Definitions.md §3.1, plus a <see cref="SortOrder"/> for
/// display ordering. Cycle prevention (a category may not be its own ancestor) is enforced in the
/// Application layer (07-ERD-Relationships.md §5.3).
/// </summary>
public sealed class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Parent category id, or <c>null</c> for a root. Self-FK, ON DELETE NO ACTION.</summary>
    public long? ParentId { get; set; }

    /// <summary>Optional short code.</summary>
    public string? Code { get; set; }

    /// <summary>Display ordering among siblings (ascending).</summary>
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    // ---- Navigations ----
    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
