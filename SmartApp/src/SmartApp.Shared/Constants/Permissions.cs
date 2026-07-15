namespace SmartApp.Shared.Constants;

/// <summary>
/// Canonical permission keys in the <c>resource.action</c> model. These are the single source of
/// truth for permission strings; the Permission reference table is seeded from them
/// (see <see cref="PermissionCatalog"/>). See SmartApp-Architecture/10-Identity-RBAC.md §2.
/// Only the Tenancy/Identity keys relevant to the current phases are declared; more are added as
/// their modules are built.
/// </summary>
public static class Permissions
{
    public static class Users
    {
        public const string View = "users.view";
        public const string Create = "users.create";
        public const string Update = "users.update";
        public const string Delete = "users.delete";
    }

    public static class Roles
    {
        public const string View = "roles.view";
        public const string Create = "roles.create";
        public const string Update = "roles.update";
        public const string Delete = "roles.delete";

        /// <summary>Assign/revoke the permissions granted to a role.</summary>
        public const string ManagePermissions = "roles.permissions.manage";
    }

    public static class Settings
    {
        public const string View = "settings.view";
        public const string Manage = "settings.manage";
    }

    public static class System
    {
        /// <summary>System-owner only — manage tenants (create/activate/suspend/disable).</summary>
        public const string ManageTenants = "system.tenants.manage";
    }
}
