using SmartApp.Domain.Identity;

namespace SmartApp.Application.Common.Interfaces;

/// <summary>
/// Issues JWT access tokens that embed the tenant id and the user's permissions, plus opaque
/// refresh tokens. Implemented in the Infrastructure layer. Phase 4 defines the contract only —
/// token issuance is wired to endpoints in a later phase. See SmartApp-Architecture/10-Identity-RBAC.md §4–5.
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Creates a signed access token for the user carrying the given permission keys.
    /// </summary>
    AccessToken CreateAccessToken(AppUser user, IReadOnlyCollection<string> permissions);

    /// <summary>Generates a new cryptographically-random raw refresh token and its SHA-256 hash.</summary>
    (string RawToken, byte[] TokenHash) CreateRefreshToken();

    /// <summary>Computes the SHA-256 hash of a raw refresh token (for lookup/comparison).</summary>
    byte[] HashRefreshToken(string rawToken);
}

/// <summary>A signed access token and its UTC expiry.</summary>
public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);
