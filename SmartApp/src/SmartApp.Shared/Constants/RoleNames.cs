namespace SmartApp.Shared.Constants;

/// <summary>
/// Default role names seeded for each tenant. See SmartApp-Architecture/10-Identity-RBAC.md §3.
/// </summary>
public static class RoleNames
{
    public const string Owner = "Owner";
    public const string Manager = "Manager";
    public const string Employee = "Employee";
    public const string Cashier = "Cashier";
    public const string Accountant = "Accountant";
}
