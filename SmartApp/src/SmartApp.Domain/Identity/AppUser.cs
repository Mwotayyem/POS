using SmartApp.Domain.Common;

namespace SmartApp.Domain.Identity;

/// <summary>
/// An application user. Users belong to a tenant, EXCEPT the system owner whose
/// <see cref="TenantId"/> is <c>null</c> (operates above tenants). Because the tenant is nullable,
/// this does not inherit <see cref="BaseEntity"/>; it composes audit + soft-delete directly and its
/// tenant query filter is applied explicitly by AppDbContext.
/// See SmartApp-Architecture/06-Tables-Definitions.md §2.1 and 10-Identity-RBAC.md.
/// </summary>
public sealed class AppUser : AuditableEntity, ISoftDeletable
{
    /// <summary>Owning tenant, or <c>null</c> for the system owner.</summary>
    public long? TenantId { get; set; }

    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;
    public bool EmailConfirmed { get; set; }

    /// <summary>True only for the platform system owner (manages tenants).</summary>
    public bool IsSystemOwner { get; set; }

    public DateTime? LastLoginAt { get; set; }

    // ---- Lockout / brute-force protection (structure) ----
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }

    // ---- ISoftDeletable ----
    public bool IsDeleted { get; set; }
    public DateTime? DeletedDate { get; set; }
    public long? DeletedBy { get; set; }

    /// <summary>Optimistic concurrency token mapped to SQL Server <c>ROWVERSION</c>.</summary>
    public byte[] ConcurrencyStamp { get; set; } = Array.Empty<byte>();

    // ---- Navigations ----
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
