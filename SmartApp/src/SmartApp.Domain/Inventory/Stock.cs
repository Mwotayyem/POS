using SmartApp.Domain.Catalog;
using SmartApp.Domain.Common;

namespace SmartApp.Domain.Inventory;

/// <summary>
/// The current stock balance of a product in a warehouse: quantity on hand and its weighted average
/// cost (WAC). One row per (product, warehouse) within a tenant. Tenant-owned. This is a mutable
/// snapshot maintained by the stock service; the immutable history lives in <see cref="StockMovement"/>.
/// See SmartApp-Architecture/06-Tables-Definitions.md (Inventory) and 05-Database-Design.md §4.
/// </summary>
public sealed class Stock : BaseEntity
{
    public long ProductId { get; set; }
    public long WarehouseId { get; set; }

    /// <summary>Current quantity on hand. DECIMAL(18,4).</summary>
    public decimal QtyOnHand { get; set; }

    /// <summary>Weighted average unit cost of the quantity on hand. DECIMAL(18,4).</summary>
    public decimal AvgCost { get; set; }

    // ---- Navigations ----
    public Product? Product { get; set; }
    public Warehouse? Warehouse { get; set; }
}
