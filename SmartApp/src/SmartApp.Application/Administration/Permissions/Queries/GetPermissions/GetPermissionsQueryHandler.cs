using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Administration.Permissions.Dtos;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Permissions.Queries.GetPermissions;

/// <summary>
/// Returns the seeded permission catalog. Permission is a global reference table (no tenant), so no
/// tenant scoping applies; results are ordered for stable, groupable display.
/// </summary>
public sealed class GetPermissionsQueryHandler
    : IRequestHandler<GetPermissionsQuery, Result<IReadOnlyList<PermissionDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetPermissionsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<PermissionDto>>> Handle(
        GetPermissionsQuery request, CancellationToken cancellationToken)
    {
        List<PermissionDto> permissions = await _db.Permissions
            .IgnoreQueryFilters()
            .OrderBy(p => p.Module).ThenBy(p => p.Code)
            .Select(p => new PermissionDto(p.Code, p.Module, p.DisplayName))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PermissionDto>>(permissions);
    }
}
