using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Users.Commands.UpdateUser;

/// <summary>
/// Updates a tenant user's profile fields and replaces its role assignments with the given set.
/// Email and password are not changed here (email is the login identity; password has its own flow).
/// </summary>
public sealed record UpdateUserCommand(
    long UserId,
    string FullName,
    string? Phone,
    IReadOnlyList<long> RoleIds) : IRequest<Result>;
