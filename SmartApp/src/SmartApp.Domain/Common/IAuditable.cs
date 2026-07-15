namespace SmartApp.Domain.Common;

/// <summary>
/// Marks an entity as carrying creation/modification audit fields.
/// These are populated automatically by the persistence layer (interceptor),
/// never set by hand in business code. Timestamps are UTC.
/// See SmartApp-Architecture/05-Database-Design.md §2.
/// </summary>
public interface IAuditable
{
    DateTime CreatedDate { get; set; }
    long? CreatedBy { get; set; }
    DateTime? ModifiedDate { get; set; }
    long? ModifiedBy { get; set; }
}
