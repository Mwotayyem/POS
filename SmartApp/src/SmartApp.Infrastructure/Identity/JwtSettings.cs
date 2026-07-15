namespace SmartApp.Infrastructure.Identity;

/// <summary>
/// Strongly-typed JWT settings bound from the "Jwt" configuration section. The signing key is a
/// secret supplied via user-secrets / environment / secret store — never committed.
/// See SmartApp-Architecture/10-Identity-RBAC.md §4 and 11-Security-Architecture.md §5.
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
