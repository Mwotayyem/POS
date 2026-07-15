namespace SmartApp.Domain.Common;

/// <summary>
/// An <see cref="Entity"/> that carries creation/modification audit fields.
/// Audit fields are populated by the persistence interceptor, not by business code.
/// Sits between <see cref="Entity"/> and <see cref="BaseEntity"/> in the hierarchy so
/// that entities needing audit without full tenant/soft-delete semantics can use it.
/// See SmartApp-Architecture/05-Database-Design.md §2.
/// </summary>
public abstract class AuditableEntity : Entity, IAuditable
{
    public DateTime CreatedDate { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public long? ModifiedBy { get; set; }
}
