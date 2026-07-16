using SmartApp.Domain.Catalog;
using SmartApp.Domain.Common;

namespace SmartApp.Domain.Sales;

/// <summary>
/// A line on a <see cref="SalesInvoice"/>. Carries two cost snapshots per
/// 06-Tables-Definitions.md §6.2: <see cref="UnitPrice"/> (the selling price at sale time) and
/// <see cref="UnitCost"/> (the weighted-average cost at sale time, for profitability reporting).
/// <see cref="ReturnedQty"/> is guarded so it never exceeds <see cref="Quantity"/>. Tenant-owned.
/// </summary>
public sealed class SalesInvoiceItem : BaseEntity
{
    public long SalesInvoiceId { get; set; }
    public long ProductId { get; set; }

    /// <summary>Sold quantity. DECIMAL(18,4).</summary>
    public decimal Quantity { get; set; }

    /// <summary>Selling unit price snapshot. DECIMAL(18,4).</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Weighted-average unit cost at sale time (profitability). DECIMAL(18,4).</summary>
    public decimal UnitCost { get; set; }

    public decimal DiscountAmount { get; set; }

    /// <summary>Tax rate percentage snapshot. DECIMAL(9,4).</summary>
    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    /// <summary>Line net total = Quantity*UnitPrice - DiscountAmount + TaxAmount. DECIMAL(18,4).</summary>
    public decimal LineTotal { get; set; }

    /// <summary>Quantity already returned by the customer (0 ≤ ReturnedQty ≤ Quantity).</summary>
    public decimal ReturnedQty { get; set; }

    // ---- Navigations ----
    public SalesInvoice? SalesInvoice { get; set; }
    public Product? Product { get; set; }
}
