using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Identity.Dtos;
using SmartApp.Domain.Identity;
using SmartApp.Domain.Tenancy;
using SmartApp.Domain.Tenancy.Enums;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Identity.Commands.Login;

/// <summary>
/// Validates credentials and tenant/user state, then issues an access + refresh token pair,
/// persisting only the refresh token hash. Login is a control-plane operation performed before any
/// tenant claim exists, so it queries across tenants (IgnoreQueryFilters) and validates ownership
/// explicitly. See SmartApp-Architecture/10-Identity-RBAC.md §4–5 and 09-Multi-Tenant.md §5.
///
/// Failure reasons are returned as <see cref="Result"/> errors (not exceptions). Credential errors
/// are intentionally generic ("invalid email or password") to avoid user enumeration.
/// </summary>
public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthTokensDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwt;
    private readonly IDateTimeProvider _clock;

    public LoginCommandHandler(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IJwtService jwt,
        IDateTimeProvider clock)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _clock = clock;
    }

    public async Task<Result<AuthTokensDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        string normalizedEmail = request.Email.Trim().ToUpperInvariant();

        AppUser? user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                u => u.NormalizedEmail == normalizedEmail && !u.IsDeleted,
                cancellationToken);

        // Generic credential error (no user enumeration).
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Result.Failure<AuthTokensDto>(Error.Unauthorized("البريد الإلكتروني أو كلمة المرور غير صحيحة."));
        }

        if (!user.IsActive)
        {
            return Result.Failure<AuthTokensDto>(Error.Forbidden("الحساب غير مُفعَّل."));
        }

        // Tenant activation gate (system owner has no tenant and skips it).
        if (!user.IsSystemOwner && user.TenantId is long tenantId)
        {
            Tenant? tenant = await _db.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, cancellationToken);

            if (tenant is null)
            {
                return Result.Failure<AuthTokensDto>(Error.Unauthorized("البريد الإلكتروني أو كلمة المرور غير صحيحة."));
            }

            if (tenant.Status != TenantStatus.Active)
            {
                return Result.Failure<AuthTokensDto>(Error.TenantInactive("الحساب غير مُفعَّل. راجع مزوّد الخدمة."));
            }
        }

        IReadOnlyCollection<string> permissions = await LoadPermissionsAsync(user, cancellationToken);

        AccessToken accessToken = _jwt.CreateAccessToken(user, permissions);
        (string rawRefresh, byte[] refreshHash) = _jwt.CreateRefreshToken();

        DateTime now = _clock.UtcNow;
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            TokenHash = refreshHash,
            CreatedAt = now,
            ExpiresAt = now.AddDays(RefreshTokenLifetimeDays),
            CreatedByIp = request.CreatedByIp,
        });

        user.LastLoginAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthTokensDto(
            accessToken.Token, accessToken.ExpiresAtUtc, rawRefresh));
    }

    private const int RefreshTokenLifetimeDays = 7;

    private async Task<IReadOnlyCollection<string>> LoadPermissionsAsync(
        AppUser user, CancellationToken cancellationToken)
    {
        // System owner has the tenant-management permission implicitly.
        // Normal users: permissions granted to any of their roles.
        var roleIds = await _db.UserRoles
            .IgnoreQueryFilters()
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

        if (roleIds.Count == 0)
        {
            return [];
        }

        var permissionCodes = await _db.RolePermissions
            .IgnoreQueryFilters()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Join(_db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        return permissionCodes;
    }
}
