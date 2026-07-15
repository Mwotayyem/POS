using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Administration.Users.Dtos;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Users.Queries.GetUserById;

/// <summary>
/// Loads one tenant user by id. The global tenant filter guarantees a user from another tenant is
/// invisible (returns NOT_FOUND rather than leaking existence).
/// </summary>
public sealed class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, Result<UserDto>>
{
    private readonly IApplicationDbContext _db;

    public GetUserByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<UserDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        UserDto? user = await _db.Users
            .Where(u => u.Id == request.UserId)
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
            .FirstOrDefaultAsync(cancellationToken);

        return user is null
            ? Result.Failure<UserDto>(Error.NotFound("المستخدم غير موجود."))
            : Result.Success(user);
    }
}
