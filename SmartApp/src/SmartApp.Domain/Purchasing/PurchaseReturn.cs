using SmartApp.Domain.Common;

namespace SmartApp.Domain.Purchasing;

/// <summary>
/// A return of previously-purchased goods to a supplier. References the original purchase invoice.
/// On creation it posts an outbound stock movement (goods leave) and reduces the supplier balance.
/// Tenant-owned. Derived from SalesReturns with purchase deltas (06-Tables-Definitions.md §6.3, §7).
/// </summary>
public sealed class PurchaseReturn : BaseEntity
{
    /// <summary>Sequential document number (e.g. PRET-000001). Unique per tenant.</summary>
    public string ReturnNumber { get; set; } = string.Empty;

    /// <summary>The original purchase invoice being returned against.</summary>
    public long PurchaseInvoiceId { get; set; }

    /// <summary>Warehouse the goods are returned from.</summary>
    public long WarehouseId { get; set; }

    public DateTime ReturnDate { get; set; }

    /// <summary>Total value of the return. DECIMAL(18,4).</summary>
    public decimal TotalAmount { get; set; }

    public string? Reason { get; set; }

    // ---- Navigations ----
    public PurchaseInvoice? PurchaseInvoice { get; set; }
    public ICollection<PurchaseReturnItem> Items { get; set; } = new List<PurchaseReturnItem>();
}
