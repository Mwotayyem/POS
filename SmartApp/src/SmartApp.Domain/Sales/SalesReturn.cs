using SmartApp.Domain.Common;

namespace SmartApp.Domain.Sales;

/// <summary>
/// A return of previously-sold goods from a customer. References the original sales invoice. On
/// creation it posts an inbound stock movement (goods come back) and reduces the customer balance.
/// Tenant-owned. Mirrors SmartApp-Architecture/06-Tables-Definitions.md §6.3.
/// </summary>
public sealed class SalesReturn : BaseEntity
{
    /// <summary>Sequential document number (e.g. SRET-000001). Unique per tenant.</summary>
    public string ReturnNumber { get; set; } = string.Empty;

    /// <summary>The original sales invoice being returned against.</summary>
    public long SalesInvoiceId { get; set; }

    /// <summary>Warehouse the goods are returned into.</summary>
    public long WarehouseId { get; set; }

    public DateTime ReturnDate { get; set; }

    /// <summary>Total value of the return. DECIMAL(18,4).</summary>
    public decimal TotalAmount { get; set; }

    public string? Reason { get; set; }

    // ---- Navigations ----
    public SalesInvoice? SalesInvoice { get; set; }
    public ICollection<SalesReturnItem> Items { get; set; } = new List<SalesReturnItem>();
}
