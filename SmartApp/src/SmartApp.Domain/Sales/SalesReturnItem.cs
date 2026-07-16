using SmartApp.Domain.Catalog;
using SmartApp.Domain.Common;

namespace SmartApp.Domain.Sales;

/// <summary>
/// A line on a <see cref="SalesReturn"/>, linked to the original sales invoice line. The
/// <see cref="UnitPrice"/> and <see cref="UnitCost"/> are frozen from that original line so the
/// return reverses stock at the original cost. Tenant-owned.
/// </summary>
public sealed class SalesReturnItem : BaseEntity
{
    public long SalesReturnId { get; set; }

    /// <summary>The original sales invoice line this return draws from.</summary>
    public long SalesInvoiceItemId { get; set; }

    public long ProductId { get; set; }

    /// <summary>Returned quantity. DECIMAL(18,4).</summary>
    public decimal Quantity { get; set; }

    /// <summary>Selling unit price frozen from the original line. DECIMAL(18,4).</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Unit cost frozen from the original line (restocked at this cost). DECIMAL(18,4).</summary>
    public decimal UnitCost { get; set; }

    /// <summary>Quantity × unit price. DECIMAL(18,4).</summary>
    public decimal LineTotal { get; set; }

    // ---- Navigations ----
    public SalesReturn? SalesReturn { get; set; }
    public SalesInvoiceItem? SalesInvoiceItem { get; set; }
    public Product? Product { get; set; }
}
