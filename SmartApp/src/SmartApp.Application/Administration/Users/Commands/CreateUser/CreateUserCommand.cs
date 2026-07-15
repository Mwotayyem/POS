using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Users.Commands.CreateUser;

/// <summary>
/// Creates a user in the current tenant and assigns the given roles. TenantId is stamped server-side.
/// Returns the new user's id. See SmartApp-Architecture/10-Identity-RBAC.md §3.
/// </summary>
public sealed record CreateUserCommand(
    string Email,
    string FullName,
    string Password,
    string? Phone,
    IReadOnlyList<long> RoleIds) : IRequest<Result<long>>;
