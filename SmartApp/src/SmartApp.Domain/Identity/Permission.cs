namespace SmartApp.Domain.Identity;

/// <summary>
/// A permission in the <c>resource.action</c> model (e.g. "products.create"). This is a global
/// reference table — it has NO tenant id and is not tenant-filtered; the fixed catalog is seeded.
/// Uses an <see cref="int"/> key (small fixed catalog), unlike the BIGINT business entities.
/// See SmartApp-Architecture/06-Tables-Definitions.md §2.3 and 10-Identity-RBAC.md §2.
/// </summary>
public sealed class Permission
{
    public int Id { get; set; }

    /// <summary>The full permission key, e.g. "products.create".</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The resource/module portion, e.g. "products".</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>Human-readable name for display.</summary>
    public string DisplayName { get; set; } = string.Empty;

    // ---- Navigations ----
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
