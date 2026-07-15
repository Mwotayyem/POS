using SmartApp.Domain.Common;

namespace SmartApp.Domain.Identity;

/// <summary>
/// A refresh token issued to a user, supporting rotation and reuse detection. The raw token is never
/// stored — <see cref="TokenHash"/> holds its SHA-256 hash. <see cref="ReplacedByTokenHash"/> links a
/// rotated token to its successor so reuse of an old token can be detected.
/// See SmartApp-Architecture/06-Tables-Definitions.md §2.6 and 10-Identity-RBAC.md §5.
/// </summary>
public sealed class RefreshToken : Entity
{
    public long UserId { get; set; }

    /// <summary>Owning tenant, or <c>null</c> for the system owner (mirrors the user).</summary>
    public long? TenantId { get; set; }

    /// <summary>SHA-256 hash of the token — the raw token is never persisted.</summary>
    public byte[] TokenHash { get; set; } = Array.Empty<byte>();

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    /// <summary>Hash of the token that replaced this one on rotation (reuse detection).</summary>
    public byte[]? ReplacedByTokenHash { get; set; }

    public string? CreatedByIp { get; set; }

    /// <summary>True when the token is neither expired nor revoked.</summary>
    public bool IsActive(DateTime utcNow) => RevokedAt is null && ExpiresAt > utcNow;

    // ---- Navigations ----
    public AppUser? User { get; set; }
}
