using SmartApp.Domain.Catalog;
using SmartApp.Domain.Common;
using SmartApp.Domain.Inventory.Enums;

namespace SmartApp.Domain.Inventory;

/// <summary>
/// An immutable, append-only stock ledger entry. Every change to a product's stock in a warehouse
/// records one movement; corrections are new reversing entries, never edits or deletes (enforced by
/// the persistence interceptor via <see cref="IAppendOnly"/>).
///
/// <para>Because it is append-only it does NOT inherit <see cref="BaseEntity"/> (no soft-delete /
/// concurrency stamp). It composes tenancy + audit directly and its tenant filter is applied
/// explicitly by AppDbContext. See SmartApp-Architecture/05-Database-Design.md §1 and
/// 13-Development-Rules.md §6.3.</para>
/// </summary>
public sealed class StockMovement : AuditableEntity, ITenantOwned, IAppendOnly
{
    /// <summary>Owning tenant. Stamped server-side.</summary>
    public long TenantId { get; set; }

    public long ProductId { get; set; }
    public long WarehouseId { get; set; }

    public StockMovementType MovementType { get; set; }

    /// <summary>
    /// Signed quantity delta applied to the balance: positive for IN / positive ADJUST / transfer-in,
    /// negative for OUT / negative ADJUST / transfer-out. DECIMAL(18,4).
    /// </summary>
    public decimal QuantityChange { get; set; }

    /// <summary>Unit cost associated with this movement (used for WAC on inbound). DECIMAL(18,4).</summary>
    public decimal UnitCost { get; set; }

    /// <summary>Quantity on hand immediately AFTER applying this movement (audit snapshot). DECIMAL(18,4).</summary>
    public decimal ResultingQty { get; set; }

    /// <summary>Weighted average cost immediately AFTER this movement (audit snapshot). DECIMAL(18,4).</summary>
    public decimal ResultingAvgCost { get; set; }

    /// <summary>When the movement occurred (UTC).</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>Optional free-text reason/notes (e.g. adjustment reason).</summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Correlates the two legs of a transfer (and, later, links to a source document such as a
    /// purchase/sales invoice). Opaque grouping key.
    /// </summary>
    public string? ReferenceCode { get; set; }

    // ---- Navigations ----
    public Product? Product { get; set; }
    public Warehouse? Warehouse { get; set; }
}
