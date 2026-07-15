using MediatR;
using SmartApp.Application.Administration.Users.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Users.Queries.GetUserById;

/// <summary>Fetches a single user of the current tenant by id. Returns NOT_FOUND if absent.</summary>
public sealed record GetUserByIdQuery(long UserId) : IRequest<Result<UserDto>>;
