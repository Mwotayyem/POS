using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Profile.Dtos;
using SmartApp.Domain.Identity;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Profile.Queries.GetMyProfile;

/// <summary>
/// Builds the caller's profile from their id (read from the authenticated principal, never from the
/// request body). Roles and effective permissions are loaded with IgnoreQueryFilters and scoped
/// explicitly to the user, so this works for both tenant users and the system owner.
/// </summary>
public sealed class GetMyProfileQueryHandler : IRequestHandler<GetMyProfileQuery, Result<ProfileDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyProfileQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<ProfileDto>> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not long userId)
        {
            return Result.Failure<ProfileDto>(Error.Unauthorized("لم يُصادَق المستخدم."));
        }

        AppUser? user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Result.Failure<ProfileDto>(Error.NotFound("المستخدم غير موجود."));
        }

        List<long> roleIds = await _db.UserRoles
            .IgnoreQueryFilters()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

        List<string> roleNames = roleIds.Count == 0
            ? []
            : await _db.Roles
                .IgnoreQueryFilters()
                .Where(r => roleIds.Contains(r.Id))
                .OrderBy(r => r.Name)
                .Select(r => r.Name)
                .ToListAsync(cancellationToken);

        List<string> permissions = roleIds.Count == 0
            ? []
            : await _db.RolePermissions
                .IgnoreQueryFilters()
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Join(_db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Code)
                .Distinct()
                .OrderBy(code => code)
                .ToListAsync(cancellationToken);

        return Result.Success(new ProfileDto(
            user.Id,
            user.Email,
            user.FullName,
            user.Phone,
            user.IsSystemOwner,
            roleNames,
            permissions));
    }
}
