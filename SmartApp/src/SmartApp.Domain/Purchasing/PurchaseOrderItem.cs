using SmartApp.Domain.Catalog;
using SmartApp.Domain.Common;

namespace SmartApp.Domain.Purchasing;

/// <summary>A line on a <see cref="PurchaseOrder"/>: a product, quantity, and unit cost. Tenant-owned.</summary>
public sealed class PurchaseOrderItem : BaseEntity
{
    public long PurchaseOrderId { get; set; }
    public long ProductId { get; set; }

    /// <summary>Ordered quantity. DECIMAL(18,4).</summary>
    public decimal Quantity { get; set; }

    /// <summary>Expected unit cost. DECIMAL(18,4).</summary>
    public decimal UnitCost { get; set; }

    /// <summary>Quantity × unit cost. DECIMAL(18,4).</summary>
    public decimal LineTotal { get; set; }

    // ---- Navigations ----
    public PurchaseOrder? PurchaseOrder { get; set; }
    public Product? Product { get; set; }
}
