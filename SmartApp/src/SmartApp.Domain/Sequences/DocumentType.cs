namespace SmartApp.Domain.Sequences;

/// <summary>
/// A document type that has its own per-tenant running number sequence (e.g. purchase invoices).
/// Stored as its numeric value (TINYINT). See SmartApp-Architecture/05-Database-Design.md §10.
/// </summary>
public enum DocumentType : byte
{
    PurchaseOrder = 1,
    PurchaseInvoice = 2,
    PurchaseReturn = 3,
    SalesInvoice = 4,
    SalesReturn = 5,
    StockAdjustment = 6,
    Payment = 7,
}
