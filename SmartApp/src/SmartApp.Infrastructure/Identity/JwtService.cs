using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Identity;

namespace SmartApp.Infrastructure.Identity;

/// <summary>
/// Issues signed JWT access tokens (embedding tenant id + permissions) and opaque refresh tokens.
/// The clock is injected so token lifetimes are testable. See SmartApp-Architecture/10-Identity-RBAC.md §4–5.
/// </summary>
public sealed class JwtService : IJwtService
{
    private readonly JwtSettings _settings;
    private readonly IDateTimeProvider _clock;

    public JwtService(IOptions<JwtSettings> settings, IDateTimeProvider clock)
    {
        _settings = settings.Value;
        _clock = clock;
    }

    public AccessToken CreateAccessToken(AppUser user, IReadOnlyCollection<string> permissions)
    {
        DateTime now = _clock.UtcNow;
        DateTime expires = now.AddMinutes(_settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)),
            new("tenant_id", user.TenantId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            new("is_system_owner", user.IsSystemOwner ? "true" : "false"),
            new(JwtRegisteredClaimNames.Name, user.FullName),
        };
        claims.AddRange(permissions.Select(p => new Claim("permissions", p)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        string token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, expires);
    }

    public (string RawToken, byte[] TokenHash) CreateRefreshToken()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(64);
        string rawToken = Convert.ToBase64String(randomBytes);
        return (rawToken, HashRefreshToken(rawToken));
    }

    public byte[] HashRefreshToken(string rawToken)
        => SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
}
