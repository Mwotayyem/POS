using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Roles.Commands.DeleteRole;

/// <summary>
/// Deletes (soft) a role of the current tenant. System roles cannot be deleted, and a role that is
/// still assigned to users cannot be deleted until those assignments are removed.
/// </summary>
public sealed record DeleteRoleCommand(long RoleId) : IRequest<Result>;
