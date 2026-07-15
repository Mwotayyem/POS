using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Roles.Commands.UpdateRole;

/// <summary>
/// Updates a role's name and description. Permissions are managed separately (assign-permissions).
/// System roles (e.g. Owner) cannot be renamed. Name stays unique within the tenant.
/// </summary>
public sealed record UpdateRoleCommand(
    long RoleId,
    string Name,
    string? Description) : IRequest<Result>;
