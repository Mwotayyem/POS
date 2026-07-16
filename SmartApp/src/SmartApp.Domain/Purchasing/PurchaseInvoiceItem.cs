using SmartApp.Domain.Catalog;
using SmartApp.Domain.Common;

namespace SmartApp.Domain.Purchasing;

/// <summary>
/// A line on a <see cref="PurchaseInvoice"/>. Per 06-Tables-Definitions.md §7, for purchases the
/// <see cref="UnitPrice"/> IS the unit cost (no separate cost snapshot). <see cref="ReturnedQty"/>
/// tracks how much of the line has been returned, guarded so it never exceeds <see cref="Quantity"/>.
/// Tenant-owned.
/// </summary>
public sealed class PurchaseInvoiceItem : BaseEntity
{
    public long PurchaseInvoiceId { get; set; }
    public long ProductId { get; set; }

    /// <summary>Purchased quantity. DECIMAL(18,4).</summary>
    public decimal Quantity { get; set; }

    /// <summary>Unit cost (the purchase price is the cost). DECIMAL(18,4).</summary>
    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    /// <summary>Tax rate percentage snapshot. DECIMAL(9,4).</summary>
    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    /// <summary>Line net total = Quantity*UnitPrice - DiscountAmount + TaxAmount. DECIMAL(18,4).</summary>
    public decimal LineTotal { get; set; }

    /// <summary>Quantity already returned to the supplier (0 ≤ ReturnedQty ≤ Quantity).</summary>
    public decimal ReturnedQty { get; set; }

    // ---- Navigations ----
    public PurchaseInvoice? PurchaseInvoice { get; set; }
    public Product? Product { get; set; }
}
