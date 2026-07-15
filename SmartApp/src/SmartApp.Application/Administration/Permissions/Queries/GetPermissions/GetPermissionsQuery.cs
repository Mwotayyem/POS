using MediatR;
using SmartApp.Application.Administration.Permissions.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Permissions.Queries.GetPermissions;

/// <summary>Lists the fixed permission catalog (read-only), ordered by module then code.</summary>
public sealed record GetPermissionsQuery : IRequest<Result<IReadOnlyList<PermissionDto>>>;
