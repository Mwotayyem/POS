namespace SmartApp.Domain.Exceptions;

/// <summary>
/// Base type for all domain-level exceptions — violations of invariants that originate
/// in the Domain layer (not infrastructure/validation concerns). The API layer maps
/// these to the unified error envelope. See SmartApp-Architecture/12-API-Architecture.md §4.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message)
        : base(message)
    {
    }

    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Stable, machine-readable error code surfaced in the API error envelope.</summary>
    public abstract string Code { get; }
}
