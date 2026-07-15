using Microsoft.AspNetCore.Authorization;

namespace SmartApp.API.Authorization;

/// <summary>
/// Succeeds when the principal carries a "permissions" claim equal to the required permission.
/// Permissions are embedded in the JWT at login (short-lived token). System owners implicitly pass.
/// See SmartApp-Architecture/10-Identity-RBAC.md §6.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        bool isSystemOwner = context.User.FindFirst("is_system_owner")?.Value == "true";

        bool hasPermission = context.User.Claims.Any(c =>
            c.Type == "permissions" && c.Value == requirement.Permission);

        if (isSystemOwner || hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
