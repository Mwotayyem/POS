using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Administration.Roles.Dtos;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Roles.Queries.GetRoleById;

/// <summary>Loads one tenant role with its permission codes; NOT_FOUND if it isn't the caller's.</summary>
public sealed class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, Result<RoleDto>>
{
    private readonly IApplicationDbContext _db;

    public GetRoleByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<RoleDto>> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        RoleDto? role = await _db.Roles
            .Where(r => r.Id == request.RoleId)
            .Select(r => new RoleDto(
                r.Id,
                r.Name,
                r.Description,
                r.IsSystemRole,
                r.RolePermissions
                    .Select(rp => rp.Permission!.Code)
                    .OrderBy(code => code)
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return role is null
            ? Result.Failure<RoleDto>(Error.NotFound("الدور غير موجود."))
            : Result.Success(role);
    }
}
