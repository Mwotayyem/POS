namespace SmartApp.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the system clock. All timestamps are UTC per
/// SmartApp-Architecture/05-Database-Design.md §1. Injected (rather than calling
/// <see cref="DateTime.UtcNow"/> directly) so time-dependent logic stays testable.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>The current instant in UTC.</summary>
    DateTime UtcNow { get; }
}
