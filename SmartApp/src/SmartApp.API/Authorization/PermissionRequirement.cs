using Microsoft.AspNetCore.Authorization;

namespace SmartApp.API.Authorization;

/// <summary>An authorization requirement for a single permission key.</summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }

    public string Permission { get; }
}
