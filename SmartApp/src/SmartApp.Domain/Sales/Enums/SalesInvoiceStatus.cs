namespace SmartApp.Domain.Sales.Enums;

/// <summary>
/// Status of a sales invoice. Values mirror the documented <c>InvoiceStatus</c>
/// (06-Tables-Definitions.md §6.1). A confirmed invoice has posted its outbound stock movement and
/// increased the customer balance. Stored as TINYINT.
/// </summary>
public enum SalesInvoiceStatus : byte
{
    Draft = 1,
    Confirmed = 2,
    PartiallyReturned = 3,
    FullyReturned = 4,
    Cancelled = 5,
}
