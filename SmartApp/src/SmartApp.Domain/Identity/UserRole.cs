using SmartApp.Domain.Common;

namespace SmartApp.Domain.Identity;

/// <summary>
/// Join entity assigning a role to a user (many-to-many). Tenant-scoped for isolation.
/// Composite key (UserId, RoleId). See SmartApp-Architecture/06-Tables-Definitions.md §2.5.
/// </summary>
public sealed class UserRole : ITenantOwned
{
    public long UserId { get; set; }
    public long RoleId { get; set; }

    public long TenantId { get; set; }

    public DateTime CreatedDate { get; set; }
    public long? CreatedBy { get; set; }

    // ---- Navigations ----
    public AppUser? User { get; set; }
    public AppRole? Role { get; set; }
}
