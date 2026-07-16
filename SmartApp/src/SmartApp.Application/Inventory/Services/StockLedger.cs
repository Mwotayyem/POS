using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Inventory.Enums;
using SmartApp.Shared.Results;
using StockEntity = SmartApp.Domain.Inventory.Stock;
using StockMovementEntity = SmartApp.Domain.Inventory.StockMovement;

namespace SmartApp.Application.Inventory.Services;

/// <summary>
/// Default <see cref="IStockLedger"/>. Loads (or creates) the Stock row for the (product, warehouse),
/// applies the signed change with WAC recomputation on inbound, records an append-only movement with
/// post-change snapshots, and keeps <c>Product.CostPrice</c> in sync with the latest average cost.
/// Stages changes only — the calling handler owns SaveChanges and the transaction.
/// </summary>
public sealed class StockLedger : IStockLedger
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly IDateTimeProvider _clock;

    public StockLedger(IApplicationDbContext db, ITenantProvider tenant, IDateTimeProvider clock)
    {
        _db = db;
        _tenant = tenant;
        _clock = clock;
    }

    public async Task<Result> ApplyAsync(
        long productId,
        long warehouseId,
        decimal quantityChange,
        decimal unitCost,
        StockMovementType movementType,
        string? reason,
        string? referenceCode,
        CancellationToken cancellationToken)
    {
        if (quantityChange == 0)
        {
            return Result.Failure(Error.Validation(
                "لا يمكن تنفيذ حركة بكمية صفر.",
                [new FieldError("quantityChange", "الكمية يجب أن تكون غير صفرية.")]));
        }

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
        {
            return Result.Failure(Error.Validation(
                "المنتج غير موجود.", [new FieldError("productId", "معرّف منتج غير صالح.")]));
        }

        bool warehouseExists = await _db.Warehouses.AnyAsync(w => w.Id == warehouseId, cancellationToken);
        if (!warehouseExists)
        {
            return Result.Failure(Error.Validation(
                "المستودع غير موجود.", [new FieldError("warehouseId", "معرّف مستودع غير صالح.")]));
        }

        StockEntity? stock = await _db.Stocks
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.WarehouseId == warehouseId, cancellationToken);

        decimal currentQty = stock?.QtyOnHand ?? 0m;
        decimal currentAvg = stock?.AvgCost ?? 0m;

        decimal newQty = currentQty + quantityChange;
        if (newQty < 0)
        {
            return Result.Failure(Error.Conflict(
                "الكمية المتوفّرة غير كافية لإتمام العملية."));
        }

        // WAC: inbound recomputes the weighted average; outbound keeps it unchanged.
        decimal newAvg = currentAvg;
        if (quantityChange > 0)
        {
            decimal inCost = unitCost < 0 ? 0m : unitCost;
            newAvg = newQty == 0
                ? 0m
                : ((currentQty * currentAvg) + (quantityChange * inCost)) / newQty;
        }
        else if (newQty == 0)
        {
            // Fully depleted — reset the average so a later inbound starts clean.
            newAvg = 0m;
        }

        if (stock is null)
        {
            stock = new StockEntity
            {
                TenantId = _tenant.CurrentTenantId,
                ProductId = productId,
                WarehouseId = warehouseId,
                QtyOnHand = newQty,
                AvgCost = newAvg,
            };
            _db.Stocks.Add(stock);
        }
        else
        {
            stock.QtyOnHand = newQty;
            stock.AvgCost = newAvg;
        }

        // Keep the product's headline cost (WAC) in sync — the docs treat Product.CostPrice as WAC.
        product.CostPrice = newAvg;

        _db.StockMovements.Add(new StockMovementEntity
        {
            TenantId = _tenant.CurrentTenantId,
            ProductId = productId,
            WarehouseId = warehouseId,
            MovementType = movementType,
            QuantityChange = quantityChange,
            UnitCost = quantityChange > 0 ? (unitCost < 0 ? 0m : unitCost) : currentAvg,
            ResultingQty = newQty,
            ResultingAvgCost = newAvg,
            OccurredAt = _clock.UtcNow,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ReferenceCode = string.IsNullOrWhiteSpace(referenceCode) ? null : referenceCode.Trim(),
        });

        return Result.Success();
    }
}
