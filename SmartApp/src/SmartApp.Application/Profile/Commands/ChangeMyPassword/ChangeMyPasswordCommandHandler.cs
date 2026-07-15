using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Identity;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Profile.Commands.ChangeMyPassword;

/// <summary>
/// Verifies the caller's current password, sets the new hash, and revokes their active refresh
/// tokens (defence in depth — a password change invalidates other live sessions). A wrong current
/// password returns UNAUTHORIZED without revealing anything further.
/// </summary>
public sealed class ChangeMyPasswordCommandHandler : IRequestHandler<ChangeMyPasswordCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeProvider _clock;

    public ChangeMyPasswordCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        IDateTimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _clock = clock;
    }

    public async Task<Result> Handle(ChangeMyPasswordCommand request, CancellationToken cancellationToken)
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

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Result.Failure(Error.Unauthorized("كلمة المرور الحالية غير صحيحة."));
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);

        await RevokeActiveTokensAsync(userId, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task RevokeActiveTokensAsync(long userId, CancellationToken cancellationToken)
    {
        DateTime now = _clock.UtcNow;
        List<RefreshToken> active = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (RefreshToken token in active)
        {
            token.RevokedAt = now;
        }
    }
}
