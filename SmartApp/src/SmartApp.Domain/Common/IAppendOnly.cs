namespace SmartApp.Domain.Common;

/// <summary>
/// Marks an entity as append-only: rows may be inserted but never updated or deleted
/// (not even soft-deleted). Any correction is a new reversing entry. Applies to
/// financial/audit ledgers such as StockMovements and AuditLogs.
/// See SmartApp-Architecture/05-Database-Design.md §1 and 13-Development-Rules.md §6.3.
/// </summary>
public interface IAppendOnly
{
}
