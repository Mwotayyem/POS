using SmartApp.Domain.Common;

namespace SmartApp.Domain.Identity;

/// <summary>
/// Join entity granting a permission to a role (many-to-many). Tenant-scoped for isolation.
/// Composite key (RoleId, PermissionId). See SmartApp-Architecture/06-Tables-Definitions.md §2.4.
/// </summary>
public sealed class RolePermission : ITenantOwned
{
    public long RoleId { get; set; }
    public int PermissionId { get; set; }

    public long TenantId { get; set; }

    public DateTime CreatedDate { get; set; }
    public long? CreatedBy { get; set; }

    // ---- Navigations ----
    public AppRole? Role { get; set; }
    public Permission? Permission { get; set; }
}
