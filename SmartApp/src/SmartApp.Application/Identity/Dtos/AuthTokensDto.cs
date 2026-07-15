namespace SmartApp.Application.Identity.Dtos;

/// <summary>
/// The token pair returned by login and refresh. The refresh token is the raw opaque value the
/// client stores; only its hash is persisted server-side. See SmartApp-Architecture/10-Identity-RBAC.md §4–5.
/// </summary>
public sealed record AuthTokensDto(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken);
