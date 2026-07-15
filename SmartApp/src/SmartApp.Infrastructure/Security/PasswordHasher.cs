using Microsoft.AspNetCore.Identity;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Identity;

namespace SmartApp.Infrastructure.Security;

/// <summary>
/// Password hashing backed by ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/> (PBKDF2).
/// Raw passwords are never stored. See SmartApp-Architecture/10-Identity-RBAC.md §7 and
/// 11-Security-Architecture.md (A02).
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<AppUser> _inner = new();

    public string Hash(string password)
        => _inner.HashPassword(user: null!, password);

    public bool Verify(string password, string passwordHash)
    {
        PasswordVerificationResult result = _inner.VerifyHashedPassword(user: null!, passwordHash, password);
        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
