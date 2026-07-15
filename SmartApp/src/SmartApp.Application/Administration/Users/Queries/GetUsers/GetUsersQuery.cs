using MediatR;
using SmartApp.Application.Administration.Users.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Users.Queries.GetUsers;

/// <summary>
/// Lists the users of the current tenant. Tenant scoping is enforced by the global query filter,
/// so no tenant id is passed from the client. Optional case-insensitive search over email/full name.
/// </summary>
public sealed record GetUsersQuery(string? Search = null) : IRequest<Result<IReadOnlyList<UserDto>>>;
