using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Administration.Users.Dtos;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Users.Queries.GetUsers;

/// <summary>
/// Loads the current tenant's users (with their role names). The tenant filter is applied globally,
/// so this query returns only rows belonging to the caller's tenant.
/// </summary>
public sealed class GetUsersQueryHandler
    : IRequestHandler<GetUsersQuery, Result<IReadOnlyList<UserDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetUsersQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<UserDto>>> Handle(
        GetUsersQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Domain.Identity.AppUser> query = _db.Users;

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // NormalizedEmail is stored upper-invariant, so match the term the same way.
            // FullName uses a LIKE pattern (provider-translated, case-insensitive per DB collation)
            // to avoid a client-side, culture-dependent ToUpper.
            string term = request.Search.Trim().ToUpperInvariant();
            string like = $"%{request.Search.Trim()}%";
            query = query.Where(u =>
                u.NormalizedEmail.Contains(term) ||
                EF.Functions.Like(u.FullName, like));
        }

        List<UserDto> users = await query
            .OrderBy(u => u.FullName)
            .Select(u => new UserDto(
                u.Id,
                u.Email,
                u.FullName,
                u.Phone,
                u.IsActive,
                u.LastLoginAt,
                u.UserRoles
                    .Select(ur => new RoleSummaryDto(ur.Role!.Id, ur.Role!.Name))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<UserDto>>(users);
    }
}
