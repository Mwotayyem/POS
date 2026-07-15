using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Roles.Commands.CreateRole;

/// <summary>
/// Creates a role in the current tenant and grants it the given permission codes (validated against
/// the fixed catalog). Returns the new role id. TenantId is stamped server-side.
/// </summary>
public sealed record CreateRoleCommand(
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions) : IRequest<Result<long>>;
