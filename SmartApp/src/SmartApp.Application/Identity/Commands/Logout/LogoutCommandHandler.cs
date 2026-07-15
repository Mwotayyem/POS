using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Identity;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Identity.Commands.Logout;

/// <summary>
/// Revokes the presented refresh token. Idempotent: an unknown or already-revoked token succeeds
/// silently (logout should never leak whether a token existed). See SmartApp-Architecture/10-Identity-RBAC.md §5.
/// </summary>
public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtService _jwt;
    private readonly IDateTimeProvider _clock;

    public LogoutCommandHandler(IApplicationDbContext db, IJwtService jwt, IDateTimeProvider clock)
    {
        _db = db;
        _jwt = jwt;
        _clock = clock;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        byte[] hash = _jwt.HashRefreshToken(request.RefreshToken);

        RefreshToken? token = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash, cancellationToken);

        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
