namespace SmartApp.Domain.Exceptions;

/// <summary>
/// Thrown when a business invariant is violated (e.g. over-returning a sold quantity,
/// negative stock). Maps to HTTP 409 / error code BUSINESS_RULE_VIOLATION.
/// See SmartApp-Architecture/12-API-Architecture.md §4 and 13-Development-Rules.md §6.
/// </summary>
public sealed class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message)
        : base(message)
    {
    }

    public override string Code => "BUSINESS_RULE_VIOLATION";
}
