using SmartApp.Domain.Catalog;
using SmartApp.Domain.Common;

namespace SmartApp.Domain.Purchasing;

/// <summary>
/// A line on a <see cref="PurchaseReturn"/>, linked to the original purchase invoice line. The
/// <see cref="UnitPrice"/> is frozen from that original line. Tenant-owned.
/// </summary>
public sealed class PurchaseReturnItem : BaseEntity
{
    public long PurchaseReturnId { get; set; }

    /// <summary>The original purchase invoice line this return draws from.</summary>
    public long PurchaseInvoiceItemId { get; set; }

    public long ProductId { get; set; }

    /// <summary>Returned quantity. DECIMAL(18,4).</summary>
    public decimal Quantity { get; set; }

    /// <summary>Unit price frozen from the original purchase line. DECIMAL(18,4).</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Quantity × unit price. DECIMAL(18,4).</summary>
    public decimal LineTotal { get; set; }

    // ---- Navigations ----
    public PurchaseReturn? PurchaseReturn { get; set; }
    public PurchaseInvoiceItem? PurchaseInvoiceItem { get; set; }
    public Product? Product { get; set; }
}
