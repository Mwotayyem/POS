using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Identity;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Profile.Commands.UpdateMyProfile;

/// <summary>Updates the caller's own name/phone. Loads the user by the authenticated principal id.</summary>
public sealed class UpdateMyProfileCommandHandler : IRequestHandler<UpdateMyProfileCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateMyProfileCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not long userId)
        {
            return Result.Failure(Error.Unauthorized("لم يُصادَق المستخدم."));
        }

        AppUser? user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Result.Failure(Error.NotFound("المستخدم غير موجود."));
        }

        user.FullName = request.FullName.Trim();
        user.Phone = request.Phone?.Trim();

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
