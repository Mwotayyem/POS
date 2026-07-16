namespace SmartApp.Domain.Inventory.Enums;

/// <summary>
/// The kind of stock movement. Movements are append-only; a correction is a new reversing entry.
/// Stored as its numeric value (TINYINT). See SmartApp-Architecture/06-Tables-Definitions.md
/// (Inventory) and 05-Database-Design.md §1.
/// </summary>
public enum StockMovementType : byte
{
    /// <summary>Goods received (purchase, return in, opening balance). Increases quantity.</summary>
    In = 1,

    /// <summary>Goods issued (sale, return out, wastage). Decreases quantity.</summary>
    Out = 2,

    /// <summary>Manual adjustment to a counted quantity (increase or decrease).</summary>
    Adjust = 3,

    /// <summary>Movement between warehouses (one OUT leg + one IN leg share a reference).</summary>
    Transfer = 4,
}
