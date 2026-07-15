using Microsoft.AspNetCore.Authorization;

namespace SmartApp.API.Authorization;

/// <summary>
/// Requires the authenticated user to hold a specific permission (e.g. "products.create").
/// The permission string becomes an authorization policy resolved dynamically by
/// <see cref="PermissionPolicyProvider"/>. See SmartApp-Architecture/10-Identity-RBAC.md §6.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "PERM:";

    public HasPermissionAttribute(string permission)
        : base(PolicyPrefix + permission)
    {
        Permission = permission;
    }

    public string Permission { get; }
}
