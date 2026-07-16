using SmartApp.Domain.Inventory.Enums;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Common.Interfaces;

/// <summary>
/// The single entry point for changing stock. Every balance change goes through here so that the
/// weighted-average-cost (WAC) recalculation, the append-only <c>StockMovements</c> entry, and the
/// <c>Product.CostPrice</c> sync happen consistently in one place. Sales/Purchases (later phases) call
/// this rather than touching Stock tables directly — matching the module-boundary rule
/// (SmartApp-Architecture/04-Domain-Boundaries.md §2.4).
///
/// <para>Callers are responsible for the surrounding transaction and for calling SaveChanges; this
/// service only stages the entity changes (updates the Stock row, adds a StockMovement).</para>
/// </summary>
public interface IStockLedger
{
    /// <summary>
    /// Applies a stock change for a product in a warehouse and records a movement.
    ///
    /// <para>WAC rule (13-Development-Rules.md §6.2): an inbound change recomputes the average cost as
    /// <c>(qty*avg + inQty*inCost) / (qty+inQty)</c>; an outbound change deducts at the current average
    /// cost and leaves the average unchanged.</para>
    /// </summary>
    /// <param name="productId">The product whose stock changes.</param>
    /// <param name="warehouseId">The warehouse holding the stock.</param>
    /// <param name="quantityChange">Signed delta: positive = inbound, negative = outbound.</param>
    /// <param name="unitCost">Unit cost for inbound movements (ignored for outbound WAC).</param>
    /// <param name="movementType">Classifies the movement (In/Out/Adjust/Transfer).</param>
    /// <param name="reason">Optional human reason (e.g. adjustment note).</param>
    /// <param name="referenceCode">Optional grouping/reference (e.g. transfer or source-doc code).</param>
    /// <returns>
    /// Failure with a business error when the change is invalid (unknown product/warehouse, or it would
    /// drive the balance negative); otherwise success. Does not call SaveChanges.
    /// </returns>
    Task<Result> ApplyAsync(
        long productId,
        long warehouseId,
        decimal quantityChange,
        decimal unitCost,
        StockMovementType movementType,
        string? reason,
        string? referenceCode,
        CancellationToken cancellationToken);
}
