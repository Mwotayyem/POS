namespace SmartApp.Application.Common.Interfaces;

/// <summary>
/// Hashes and verifies user passwords. Implemented in the Infrastructure layer.
/// Raw passwords are never stored — only the hash. See SmartApp-Architecture/10-Identity-RBAC.md §7.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Produces a salted hash for the given plaintext password.</summary>
    string Hash(string password);

    /// <summary>Verifies a plaintext password against a stored hash.</summary>
    bool Verify(string password, string passwordHash);
}
