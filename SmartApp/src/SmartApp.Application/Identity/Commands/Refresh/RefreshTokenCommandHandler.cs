using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Identity.Dtos;
using SmartApp.Domain.Identity;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Identity.Commands.Refresh;

/// <summary>
/// Rotates a refresh token: validates the presented token, revokes it, issues a new access + refresh
/// pair, and links old→new for reuse detection. If a token that was already revoked is presented, that
/// is treated as a reuse/theft signal and the whole chain for the user is revoked.
/// See SmartApp-Architecture/10-Identity-RBAC.md §5.
/// </summary>
public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthTokensDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtService _jwt;
    private readonly IDateTimeProvider _clock;

    public RefreshTokenCommandHandler(IApplicationDbContext db, IJwtService jwt, IDateTimeProvider clock)
    {
        _db = db;
        _jwt = jwt;
        _clock = clock;
    }

    public async Task<Result<AuthTokensDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        byte[] presentedHash = _jwt.HashRefreshToken(request.RefreshToken);
        DateTime now = _clock.UtcNow;

        RefreshToken? token = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(rt => rt.TokenHash == presentedHash, cancellationToken);

        if (token is null)
        {
            return Result.Failure<AuthTokensDto>(Error.Unauthorized("توكن التحديث غير صالح."));
        }

        // Reuse detection: a revoked token being presented again → revoke the user's whole chain.
        if (token.RevokedAt is not null)
        {
            await RevokeAllForUserAsync(token.UserId, now, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthTokensDto>(Error.Unauthorized("توكن التحديث غير صالح."));
        }

        if (!token.IsActive(now))
        {
            return Result.Failure<AuthTokensDto>(Error.Unauthorized("انتهت صلاحية توكن التحديث."));
        }

        AppUser? user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == token.UserId && !u.IsDeleted, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return Result.Failure<AuthTokensDto>(Error.Unauthorized("توكن التحديث غير صالح."));
        }

        // Issue the new pair.
        IReadOnlyCollection<string> permissions = await LoadPermissionsAsync(user, cancellationToken);
        AccessToken accessToken = _jwt.CreateAccessToken(user, permissions);
        (string rawRefresh, byte[] newHash) = _jwt.CreateRefreshToken();

        // Rotate: revoke old, link to new, persist new.
        token.RevokedAt = now;
        token.ReplacedByTokenHash = newHash;

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            TokenHash = newHash,
            CreatedAt = now,
            ExpiresAt = now.AddDays(RefreshTokenLifetimeDays),
            CreatedByIp = request.CreatedByIp,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthTokensDto(
            accessToken.Token, accessToken.ExpiresAtUtc, rawRefresh));
    }

    private const int RefreshTokenLifetimeDays = 7;

    private async Task RevokeAllForUserAsync(long userId, DateTime now, CancellationToken cancellationToken)
    {
        var active = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (RefreshToken rt in active)
        {
            rt.RevokedAt = now;
        }
    }

    private async Task<IReadOnlyCollection<string>> LoadPermissionsAsync(
        AppUser user, CancellationToken cancellationToken)
    {
        var roleIds = await _db.UserRoles
            .IgnoreQueryFilters()
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

        if (roleIds.Count == 0)
        {
            return [];
        }

        return await _db.RolePermissions
            .IgnoreQueryFilters()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Join(_db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Code)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
