using SmartApp.Shared.Results;

namespace SmartApp.Application.Common.Exceptions;

/// <summary>
/// Thrown by the validation pipeline behavior when FluentValidation rules fail. Carries the
/// field-level errors so the API can render them in the error envelope (400 VALIDATION_ERROR).
/// See SmartApp-Architecture/12-API-Architecture.md §4.
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException(IReadOnlyList<FieldError> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IReadOnlyList<FieldError> Errors { get; }
}
