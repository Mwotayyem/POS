using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SmartApp.API.Authorization;
using SmartApp.Application.Administration.Permissions.Queries.GetPermissions;
using SmartApp.Shared.Constants;

namespace SmartApp.API.Controllers.v1;

/// <summary>
/// Read-only view of the fixed permission catalog. Permissions are seeded, never created at runtime;
/// assignment happens by granting them to roles (see <see cref="RolesController"/>).
/// See SmartApp-Architecture/10-Identity-RBAC.md §2.
/// </summary>
[ApiVersion("1.0")]
public sealed class PermissionsController : ApiControllerBase
{
    /// <summary>Lists all permissions in the catalog (grouped client-side by module).</summary>
    [HttpGet]
    [HasPermission(Permissions.Roles.View)]
    public async Task<IActionResult> GetPermissions(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetPermissionsQuery(), cancellationToken);
        return ToResponse(result);
    }
}
