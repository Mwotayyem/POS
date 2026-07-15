namespace SmartApp.API.Configuration;

/// <summary>
/// Strongly-typed JWT settings bound from configuration ("Jwt" section).
/// Placeholder structure for Phase 1 — consumed by authentication in Phase 3.
/// Secrets (SigningKey) are supplied via user-secrets / environment / secret store,
/// never committed — see SmartApp-Architecture/11-Security-Architecture.md §5.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 7;
}
