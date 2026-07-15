using SmartApp.Application.Common.Interfaces;

namespace SmartApp.Infrastructure.Services;

/// <summary>
/// Default clock — returns UTC. See SmartApp-Architecture/05-Database-Design.md §1.
/// </summary>
public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
