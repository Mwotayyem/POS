using SmartApp.Domain.Common;
using SmartApp.Domain.Purchasing.Enums;

namespace SmartApp.Domain.Purchasing;

/// <summary>
/// A purchase order — a plan to buy goods from a supplier (greenfield; not in the architecture docs).
/// It does NOT affect stock; receiving is performed by creating a purchase invoice. Tenant-owned.
/// </summary>
public sealed class PurchaseOrder : BaseEntity
{
    /// <summary>Sequential document number (e.g. PO-000001).</summary>
    public string OrderNumber { get; set; } = string.Empty;

    public long SupplierId { get; set; }

    public DateTime OrderDate { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    /// <summary>Sum of line totals. DECIMAL(18,4).</summary>
    public decimal Total { get; set; }

    public string? Notes { get; set; }

    // ---- Navigations ----
    public Supplier? Supplier { get; set; }
    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}
