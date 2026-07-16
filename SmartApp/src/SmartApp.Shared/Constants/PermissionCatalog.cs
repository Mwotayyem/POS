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

        // ---- Catalog: Categories ----
        new(Permissions.Categories.View, "catalog", "عرض التصنيفات"),
        new(Permissions.Categories.Create, "catalog", "إنشاء تصنيف"),
        new(Permissions.Categories.Update, "catalog", "تعديل تصنيف"),
        new(Permissions.Categories.Delete, "catalog", "حذف تصنيف"),

        // ---- Catalog: Units ----
        new(Permissions.Units.View, "catalog", "عرض وحدات القياس"),
        new(Permissions.Units.Create, "catalog", "إنشاء وحدة قياس"),
        new(Permissions.Units.Update, "catalog", "تعديل وحدة قياس"),
        new(Permissions.Units.Delete, "catalog", "حذف وحدة قياس"),

        // ---- Catalog: Brands ----
        new(Permissions.Brands.View, "catalog", "عرض العلامات التجارية"),
        new(Permissions.Brands.Create, "catalog", "إنشاء علامة تجارية"),
        new(Permissions.Brands.Update, "catalog", "تعديل علامة تجارية"),
        new(Permissions.Brands.Delete, "catalog", "حذف علامة تجارية"),

        // ---- Catalog: Products (incl. barcodes & prices, managed under the product) ----
        new(Permissions.Products.View, "catalog", "عرض المنتجات"),
        new(Permissions.Products.Create, "catalog", "إنشاء منتج"),
        new(Permissions.Products.Update, "catalog", "تعديل منتج"),
        new(Permissions.Products.Delete, "catalog", "حذف منتج"),

        // ---- Inventory: Warehouses ----
        new(Permissions.Warehouses.View, "inventory", "عرض المستودعات"),
        new(Permissions.Warehouses.Create, "inventory", "إنشاء مستودع"),
        new(Permissions.Warehouses.Update, "inventory", "تعديل مستودع"),
        new(Permissions.Warehouses.Delete, "inventory", "حذف مستودع"),

        // ---- Inventory: Stock ----
        new(Permissions.Stock.View, "inventory", "عرض المخزون والحركات"),
        new(Permissions.Stock.Adjust, "inventory", "تسوية المخزون"),
        new(Permissions.Stock.Transfer, "inventory", "تحويل المخزون بين المستودعات"),

        // ---- Purchasing: Suppliers ----
        new(Permissions.Suppliers.View, "purchasing", "عرض المورّدين"),
        new(Permissions.Suppliers.Create, "purchasing", "إنشاء مورّد"),
        new(Permissions.Suppliers.Update, "purchasing", "تعديل مورّد"),
        new(Permissions.Suppliers.Delete, "purchasing", "حذف مورّد"),

        // ---- Purchasing: Orders / Invoices / Returns ----
        new(Permissions.Purchases.View, "purchasing", "عرض المشتريات"),
        new(Permissions.Purchases.Create, "purchasing", "إنشاء مستند شراء"),
        new(Permissions.Purchases.Update, "purchasing", "تعديل مستند شراء"),
        new(Permissions.Purchases.Delete, "purchasing", "حذف مستند شراء"),
        new(Permissions.Purchases.Post, "purchasing", "ترحيل/استلام مستند شراء"),

        // ---- Sales: Customers ----
        new(Permissions.Customers.View, "sales", "عرض العملاء"),
        new(Permissions.Customers.Create, "sales", "إنشاء عميل"),
        new(Permissions.Customers.Update, "sales", "تعديل عميل"),
        new(Permissions.Customers.Delete, "sales", "حذف عميل"),

        // ---- Sales: Invoices / Payments / Returns ----
        new(Permissions.Sales.View, "sales", "عرض المبيعات"),
        new(Permissions.Sales.Create, "sales", "إنشاء فاتورة مبيعات"),
        new(Permissions.Sales.Update, "sales", "تعديل مستند مبيعات"),
        new(Permissions.Sales.Delete, "sales", "حذف مستند مبيعات"),
        new(Permissions.Sales.Post, "sales", "دفعات/مرتجعات المبيعات"),

        // ---- System (owner only) ----
        new(Permissions.System.ManageTenants, "system", "إدارة المستأجرين"),
    ];

    /// <summary>Just the permission codes (used to grant every permission to the Owner role).</summary>
    public static IEnumerable<string> AllCodes => All.Select(p => p.Code);
}
