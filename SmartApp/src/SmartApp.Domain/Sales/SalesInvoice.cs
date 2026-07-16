using SmartApp.Domain.Common;
using SmartApp.Domain.Sales.Enums;

namespace SmartApp.Domain.Sales;

/// <summary>
/// A sales invoice to a customer (or a cash sale when <see cref="CustomerId"/> is null). When
/// confirmed, it posts an outbound stock movement per line and increases the customer balance — all
/// in one transaction. Tenant-owned. Mirrors SmartApp-Architecture/06-Tables-Definitions.md §6.1.
/// </summary>
public sealed class SalesInvoice : BaseEntity
{
    /// <summary>Sequential document number (e.g. SINV-000001). Unique per tenant.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>Customer, or <c>null</c> for a cash sale with no registered customer.</summary>
    public long? CustomerId { get; set; }

    /// <summary>Warehouse the goods are issued from.</summary>
    public long WarehouseId { get; set; }

    public DateTime InvoiceDate { get; set; }

    public SalesInvoiceStatus Status { get; set; } = SalesInvoiceStatus.Draft;

    public decimal SubTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }

    /// <summary>Amount already collected from the customer against this invoice. DECIMAL(18,4).</summary>
    public decimal PaidAmount { get; set; }

    public string? Notes { get; set; }

    // ---- Navigations ----
    public Customer? Customer { get; set; }
    public ICollection<SalesInvoiceItem> Items { get; set; } = new List<SalesInvoiceItem>();
}
