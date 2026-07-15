using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SmartApp.API.Authorization;
using SmartApp.Application.Administration.Users.Commands.CreateUser;
using SmartApp.Application.Administration.Users.Commands.SetUserActive;
using SmartApp.Application.Administration.Users.Commands.UpdateUser;
using SmartApp.Application.Administration.Users.Queries.GetUserById;
using SmartApp.Application.Administration.Users.Queries.GetUsers;
using SmartApp.Shared.Constants;

namespace SmartApp.API.Controllers.v1;

/// <summary>
/// Tenant user administration: list, get, create, update, activate/deactivate. Every action is
/// guarded by a <see cref="HasPermissionAttribute"/> permission and scoped to the caller's tenant.
/// See SmartApp-Architecture/10-Identity-RBAC.md §6 and 12-API-Architecture.md.
/// </summary>
[ApiVersion("1.0")]
public sealed class UsersController : ApiControllerBase
{
    /// <summary>Lists the current tenant's users, with optional email/name search.</summary>
    [HttpGet]
    [HasPermission(Permissions.Users.View)]
    public async Task<IActionResult> GetUsers([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetUsersQuery(search), cancellationToken);
        return ToResponse(result);
    }

    /// <summary>Gets a single user of the current tenant by id.</summary>
    [HttpGet("{id:long}")]
    [HasPermission(Permissions.Users.View)]
    public async Task<IActionResult> GetUser(long id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetUserByIdQuery(id), cancellationToken);
        return ToResponse(result);
    }

    /// <summary>Creates a user and assigns the given roles. Returns the new user id.</summary>
    [HttpPost]
    [HasPermission(Permissions.Users.Create)]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new CreateUserCommand(
                request.Email, request.FullName, request.Password, request.Phone, request.RoleIds ?? []),
            cancellationToken);
        return ToResponse(result);
    }

    /// <summary>Updates a user's name/phone and replaces its role assignments.</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Users.Update)]
    public async Task<IActionResult> UpdateUser(
        long id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new UpdateUserCommand(id, request.FullName, request.Phone, request.RoleIds ?? []),
            cancellationToken);
        return ToResponse(result);
    }

    /// <summary>Activates a user.</summary>
    [HttpPost("{id:long}/activate")]
    [HasPermission(Permissions.Users.Update)]
    public async Task<IActionResult> ActivateUser(long id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new SetUserActiveCommand(id, IsActive: true), cancellationToken);
        return ToResponse(result);
    }

    /// <summary>Deactivates a user (also revokes their active refresh tokens).</summary>
    [HttpPost("{id:long}/deactivate")]
    [HasPermission(Permissions.Users.Update)]
    public async Task<IActionResult> DeactivateUser(long id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new SetUserActiveCommand(id, IsActive: false), cancellationToken);
        return ToResponse(result);
    }
}

/// <summary>Create-user request body.</summary>
public sealed record CreateUserRequest(
    string Email, string FullName, string Password, string? Phone, IReadOnlyList<long>? RoleIds);

/// <summary>Update-user request body.</summary>
public sealed record UpdateUserRequest(string FullName, string? Phone, IReadOnlyList<long>? RoleIds);
