namespace SmartApp.Domain.Purchasing.Enums;

/// <summary>
/// Status of a purchase invoice. Mirrors the documented Sales <c>InvoiceStatus</c> values
/// (06-Tables-Definitions.md §6.1). A confirmed invoice has posted its inbound stock movement and
/// updated WAC + the supplier balance. Stored as TINYINT.
/// </summary>
public enum PurchaseInvoiceStatus : byte
{
    /// <summary>Editable draft; no stock/balance effect yet.</summary>
    Draft = 1,

    /// <summary>Posted: stock received (inbound movement), WAC and supplier balance updated.</summary>
    Confirmed = 2,

    /// <summary>Some quantity has been returned to the supplier.</summary>
    PartiallyReturned = 3,

    /// <summary>All quantity has been returned.</summary>
    FullyReturned = 4,

    /// <summary>Cancelled.</summary>
    Cancelled = 5,
}
