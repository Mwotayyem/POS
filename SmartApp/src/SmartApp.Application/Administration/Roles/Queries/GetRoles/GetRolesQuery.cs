using MediatR;
using SmartApp.Application.Administration.Roles.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Roles.Queries.GetRoles;

/// <summary>Lists all roles of the current tenant with their granted permission codes.</summary>
public sealed record GetRolesQuery : IRequest<Result<IReadOnlyList<RoleDto>>>;
