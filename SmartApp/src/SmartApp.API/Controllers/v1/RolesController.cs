using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SmartApp.API.Authorization;
using SmartApp.Application.Administration.Roles.Commands.CreateRole;
using SmartApp.Application.Administration.Roles.Commands.DeleteRole;
using SmartApp.Application.Administration.Roles.Commands.SetRolePermissions;
using SmartApp.Application.Administration.Roles.Commands.UpdateRole;
using SmartApp.Application.Administration.Roles.Queries.GetRoleById;
using SmartApp.Application.Administration.Roles.Queries.GetRoles;
using SmartApp.Shared.Constants;

namespace SmartApp.API.Controllers.v1;

/// <summary>
/// Role administration (RBAC): CRUD roles and assign their permissions. Permissions themselves are a
/// fixed catalog (read-only) — this manages which of them each role holds.
/// See SmartApp-Architecture/10-Identity-RBAC.md §3, §6.
/// </summary>
[ApiVersion("1.0")]
public sealed class RolesController : ApiControllerBase
{
    /// <summary>Lists the current tenant's roles with their permission codes.</summary>
    [HttpGet]
    [HasPermission(Permissions.Roles.View)]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetRolesQuery(), cancellationToken);
        return ToResponse(result);
    }

    /// <summary>Gets a single role by id.</summary>
    [HttpGet("{id:long}")]
    [HasPermission(Permissions.Roles.View)]
    public async Task<IActionResult> GetRole(long id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetRoleByIdQuery(id), cancellationToken);
        return ToResponse(result);
    }

    /// <summary>Creates a role and grants it the given permissions. Returns the new role id.</summary>
    [HttpPost]
    [HasPermission(Permissions.Roles.Create)]
    public async Task<IActionResult> CreateRole(
        [FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new CreateRoleCommand(request.Name, request.Description, request.Permissions ?? []),
            cancellationToken);
        return ToResponse(result);
    }

    /// <summary>Renames a role / updates its description.</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Roles.Update)]
    public async Task<IActionResult> UpdateRole(
        long id, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new UpdateRoleCommand(id, request.Name, request.Description), cancellationToken);
        return ToResponse(result);
    }

    /// <summary>Deletes (soft) a role. System roles and roles still assigned to users are refused.</summary>
    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Roles.Delete)]
    public async Task<IActionResult> DeleteRole(long id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteRoleCommand(id), cancellationToken);
        return ToResponse(result);
    }

    /// <summary>Replaces the full set of permissions granted to a role.</summary>
    [HttpPut("{id:long}/permissions")]
    [HasPermission(Permissions.Roles.ManagePermissions)]
    public async Task<IActionResult> SetRolePermissions(
        long id, [FromBody] SetRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new SetRolePermissionsCommand(id, request.Permissions ?? []), cancellationToken);
        return ToResponse(result);
    }
}

/// <summary>Create-role request body.</summary>
public sealed record CreateRoleRequest(string Name, string? Description, IReadOnlyList<string>? Permissions);

/// <summary>Update-role request body.</summary>
public sealed record UpdateRoleRequest(string Name, string? Description);

/// <summary>Set-role-permissions request body.</summary>
public sealed record SetRolePermissionsRequest(IReadOnlyList<string>? Permissions);
