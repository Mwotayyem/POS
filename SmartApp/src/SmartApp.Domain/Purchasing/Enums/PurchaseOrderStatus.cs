namespace SmartApp.Domain.Purchasing.Enums;

/// <summary>
/// Lifecycle of a purchase order (greenfield — not in the architecture docs). A PO plans a purchase;
/// it does not affect stock. Receiving is done by creating a purchase invoice. Stored as TINYINT.
/// </summary>
public enum PurchaseOrderStatus : byte
{
    /// <summary>Editable draft.</summary>
    Draft = 1,

    /// <summary>Confirmed and sent to the supplier; awaiting goods.</summary>
    Confirmed = 2,

    /// <summary>Goods received (a purchase invoice has been created from it).</summary>
    Received = 3,

    /// <summary>Cancelled; no further action.</summary>
    Cancelled = 4,
}
