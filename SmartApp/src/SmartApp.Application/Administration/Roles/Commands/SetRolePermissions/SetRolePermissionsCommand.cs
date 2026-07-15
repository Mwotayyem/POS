using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Roles.Commands.SetRolePermissions;

/// <summary>
/// Replaces the full set of permissions granted to a role with the provided codes (idempotent
/// reconcile: adds missing grants, removes extra). Codes are validated against the fixed catalog.
/// Backs the role permission-assignment endpoint. See SmartApp-Architecture/10-Identity-RBAC.md §6.
/// </summary>
public sealed record SetRolePermissionsCommand(
    long RoleId,
    IReadOnlyList<string> Permissions) : IRequest<Result>;
