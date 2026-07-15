namespace SmartApp.Shared.Results;

/// <summary>
/// A machine-readable error: a stable <see cref="Code"/> plus a human message and optional
/// field-level details. See SmartApp-Architecture/12-API-Architecture.md §2, §4.
/// </summary>
public sealed record Error(string Code, string Message, IReadOnlyList<FieldError>? Details = null)
{
    public static Error Validation(string message, IReadOnlyList<FieldError> details)
        => new("VALIDATION_ERROR", message, details);

    public static Error NotFound(string message) => new("NOT_FOUND", message);
    public static Error Unauthorized(string message) => new("UNAUTHORIZED", message);
    public static Error Forbidden(string message) => new("FORBIDDEN", message);
    public static Error TenantInactive(string message) => new("TENANT_INACTIVE", message);
    public static Error Conflict(string message) => new("BUSINESS_RULE_VIOLATION", message);
    public static Error Internal(string message) => new("INTERNAL_ERROR", message);
}

/// <summary>A validation error tied to a specific input field.</summary>
public sealed record FieldError(string Field, string Message);
