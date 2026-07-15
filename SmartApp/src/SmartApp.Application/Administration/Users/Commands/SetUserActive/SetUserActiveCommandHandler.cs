using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Identity;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Users.Commands.SetUserActive;

/// <summary>
/// Toggles a user's active flag. On deactivation, revokes the user's currently-active refresh tokens
/// so a live session cannot be extended after the account is disabled. Idempotent — setting the same
/// state again is a no-op success.
/// </summary>
public sealed class SetUserActiveCommandHandler : IRequestHandler<SetUserActiveCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public SetUserActiveCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result> Handle(SetUserActiveCommand request, CancellationToken cancellationToken)
    {
        AppUser? user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(Error.NotFound("المستخدم غير موجود."));
        }

        if (user.IsActive == request.IsActive)
        {
            return Result.Success();
        }

        user.IsActive = request.IsActive;

        if (!request.IsActive)
        {
            await RevokeActiveTokensAsync(user.Id, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task RevokeActiveTokensAsync(long userId, CancellationToken cancellationToken)
    {
        DateTime now = _clock.UtcNow;
        List<RefreshToken> active = await _db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (RefreshToken token in active)
        {
            token.RevokedAt = now;
        }
    }
}
