namespace SmartApp.Shared.Constants;

/// <summary>
/// A single permission definition in the fixed catalog: the <see cref="Code"/> (resource.action),
/// its owning <see cref="Module"/>, and a human-readable <see cref="DisplayName"/>.
/// </summary>
public sealed record PermissionDefinition(string Code, string Module, string DisplayName);

/// <summary>
/// The complete, fixed catalog of permissions the platform ships with. The Permission reference
/// table is seeded from this list (idempotently) at startup, and the default Owner role is granted
/// every code here. Permissions are NOT created at runtime — this is the source of truth.
/// See SmartApp-Architecture/10-Identity-RBAC.md §2.
/// </summary>
public static class PermissionCatalog
{
    /// <summary>All permission definitions, grouped conceptually by module.</summary>
    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        // ---- Users ----
        new(Permissions.Users.View, "users", "عرض المستخدمين"),
        new(Permissions.Users.Create, "users", "إنشاء مستخدم"),
        new(Permissions.Users.Update, "users", "تعديل مستخدم"),
        new(Permissions.Users.Delete, "users", "حذف مستخدم"),

        // ---- Roles ----
        new(Permissions.Roles.View, "roles", "عرض الأدوار"),
        new(Permissions.Roles.Create, "roles", "إنشاء دور"),
        new(Permissions.Roles.Update, "roles", "تعديل دور"),
        new(Permissions.Roles.Delete, "roles", "حذف دور"),
        new(Permissions.Roles.ManagePermissions, "roles", "إدارة صلاحيات الدور"),

        // ---- Settings ----
        new(Permissions.Settings.View, "settings", "عرض إعدادات المستأجر"),
        new(Permissions.Settings.Manage, "settings", "تعديل إعدادات المستأجر"),

        // ---- System (owner only) ----
        new(Permissions.System.ManageTenants, "system", "إدارة المستأجرين"),
    ];

    /// <summary>Just the permission codes (used to grant every permission to the Owner role).</summary>
    public static IEnumerable<string> AllCodes => All.Select(p => p.Code);
}
