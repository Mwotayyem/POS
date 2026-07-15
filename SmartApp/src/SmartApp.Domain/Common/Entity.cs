namespace SmartApp.Domain.Common;

/// <summary>
/// Base class for all persisted entities: the primary key only.
/// Key is <c>BIGINT IDENTITY</c> per SmartApp-Architecture/05-Database-Design.md §4.
/// </summary>
public abstract class Entity
{
    public long Id { get; set; }
}
