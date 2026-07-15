using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Administration.Roles.Dtos;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Roles.Queries.GetRoles;

/// <summary>
/// Loads the current tenant's roles with their permission codes. Roles and their permission grants
/// are tenant-filtered; Permission is a global reference joined via the navigation.
/// </summary>
public sealed class GetRolesQueryHandler
    : IRequestHandler<GetRolesQuery, Result<IReadOnlyList<RoleDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetRolesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<RoleDto>>> Handle(
        GetRolesQuery request, CancellationToken cancellationToken)
    {
        List<RoleDto> roles = await _db.Roles
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(
                r.Id,
                r.Name,
                r.Description,
                r.IsSystemRole,
                r.RolePermissions
                    .Select(rp => rp.Permission!.Code)
                    .OrderBy(code => code)
                    .ToList()))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<RoleDto>>(roles);
    }
}
