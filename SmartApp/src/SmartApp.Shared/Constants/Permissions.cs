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

    // ---- Catalog module (Phase 7) ----

    public static class Categories
    {
        public const string View = "catalog.categories.view";
        public const string Create = "catalog.categories.create";
        public const string Update = "catalog.categories.update";
        public const string Delete = "catalog.categories.delete";
    }

    public static class Units
    {
        public const string View = "catalog.units.view";
        public const string Create = "catalog.units.create";
        public const string Update = "catalog.units.update";
        public const string Delete = "catalog.units.delete";
    }

    public static class Brands
    {
        public const string View = "catalog.brands.view";
        public const string Create = "catalog.brands.create";
        public const string Update = "catalog.brands.update";
        public const string Delete = "catalog.brands.delete";
    }

    public static class Products
    {
        public const string View = "catalog.products.view";
        public const string Create = "catalog.products.create";
        public const string Update = "catalog.products.update";
        public const string Delete = "catalog.products.delete";
    }

    // ---- Inventory module (Phase 8) ----

    public static class Warehouses
    {
        public const string View = "inventory.warehouses.view";
        public const string Create = "inventory.warehouses.create";
        public const string Update = "inventory.warehouses.update";
        public const string Delete = "inventory.warehouses.delete";
    }

    public static class Stock
    {
        /// <summary>View stock balances and movement history.</summary>
        public const string View = "inventory.stock.view";

        /// <summary>Perform stock adjustments (increase/decrease with a reason).</summary>
        public const string Adjust = "inventory.stock.adjust";

        /// <summary>Transfer stock between warehouses.</summary>
        public const string Transfer = "inventory.stock.transfer";
    }

    // ---- Purchasing module (Phase 9) ----

    public static class Suppliers
    {
        public const string View = "purchasing.suppliers.view";
        public const string Create = "purchasing.suppliers.create";
        public const string Update = "purchasing.suppliers.update";
        public const string Delete = "purchasing.suppliers.delete";
    }

    public static class Purchases
    {
        public const string View = "purchasing.view";
        public const string Create = "purchasing.create";
        public const string Update = "purchasing.update";
        public const string Delete = "purchasing.delete";

        /// <summary>Post/receive a purchase document (affects inventory).</summary>
        public const string Post = "purchasing.post";
    }

    // ---- Sales module (Phase 10) ----

    public static class Customers
    {
        public const string View = "sales.customers.view";
        public const string Create = "sales.customers.create";
        public const string Update = "sales.customers.update";
        public const string Delete = "sales.customers.delete";
    }

    public static class Sales
    {
        public const string View = "sales.view";
        public const string Create = "sales.create";
        public const string Update = "sales.update";
        public const string Delete = "sales.delete";

        /// <summary>Post a sale / record a payment / process a return (affects inventory or balances).</summary>
        public const string Post = "sales.post";
    }

    public static class System
    {
        /// <summary>System-owner only — manage tenants (create/activate/suspend/disable).</summary>
        public const string ManageTenants = "system.tenants.manage";
    }
}
