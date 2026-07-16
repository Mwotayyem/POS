using SmartApp.Domain.Common;
using SmartApp.Domain.Purchasing.Enums;

namespace SmartApp.Domain.Purchasing;

/// <summary>
/// A purchase invoice from a supplier. When confirmed, it posts an inbound stock movement (updating
/// WAC) and increases the supplier balance — all in one transaction. Tenant-owned. Header columns
/// mirror SalesInvoices with SupplierId (06-Tables-Definitions.md §6.1 + §7 deltas).
/// </summary>
public sealed class PurchaseInvoice : BaseEntity
{
    /// <summary>Sequential document number (e.g. PINV-000001). Unique per tenant.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    public long SupplierId { get; set; }

    /// <summary>Warehouse the goods are received into.</summary>
    public long WarehouseId { get; set; }

    public DateTime InvoiceDate { get; set; }

    public PurchaseInvoiceStatus Status { get; set; } = PurchaseInvoiceStatus.Draft;

    public decimal SubTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }

    /// <summary>Amount already paid to the supplier against this invoice. DECIMAL(18,4).</summary>
    public decimal PaidAmount { get; set; }

    public string? Notes { get; set; }

    // ---- Navigations ----
    public Supplier? Supplier { get; set; }
    public ICollection<PurchaseInvoiceItem> Items { get; set; } = new List<PurchaseInvoiceItem>();
}
