using SmartApp.Domain.Common;

namespace SmartApp.Domain.Identity;

/// <summary>
/// A role within a tenant. Roles are containers of permissions (RBAC). Tenant-owned, so it inherits
/// <see cref="BaseEntity"/> and is globally tenant-filtered.
/// See SmartApp-Architecture/06-Tables-Definitions.md §2.2 and 10-Identity-RBAC.md §3.
/// </summary>
public sealed class AppRole : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Default roles that ship with each tenant and cannot be deleted.</summary>
    public bool IsSystemRole { get; set; }

    // ---- Navigations ----
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
