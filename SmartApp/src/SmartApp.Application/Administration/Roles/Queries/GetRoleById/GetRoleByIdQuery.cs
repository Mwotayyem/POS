using MediatR;
using SmartApp.Application.Administration.Roles.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Roles.Queries.GetRoleById;

/// <summary>Fetches one role of the current tenant by id. Returns NOT_FOUND if absent.</summary>
public sealed record GetRoleByIdQuery(long RoleId) : IRequest<Result<RoleDto>>;
