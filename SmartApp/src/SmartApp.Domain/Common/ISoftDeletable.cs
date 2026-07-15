namespace SmartApp.Domain.Common;

/// <summary>
/// Marks an entity as soft-deletable. Deletes never remove the row physically;
/// the persistence layer flips <see cref="IsDeleted"/> and stamps the delete audit
/// fields. Soft-deleted rows are excluded by the global query filter.
/// See SmartApp-Architecture/05-Database-Design.md §1 and 09-Multi-Tenant.md §6.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedDate { get; set; }
    long? DeletedBy { get; set; }
}
