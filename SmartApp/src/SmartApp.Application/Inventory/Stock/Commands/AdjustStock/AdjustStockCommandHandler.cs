using MediatR;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Inventory.Enums;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Inventory.Stock.Commands.AdjustStock;

/// <summary>
/// Applies a stock adjustment through the stock ledger (which performs WAC recomputation and records
/// the append-only movement), then commits. The ledger rejects an adjustment that would drive the
/// balance negative or references an unknown product/warehouse.
/// </summary>
public sealed class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, Result>
{
    private readonly IStockLedger _ledger;
    private readonly IApplicationDbContext _db;

    public AdjustStockCommandHandler(IStockLedger ledger, IApplicationDbContext db)
    {
        _ledger = ledger;
        _db = db;
    }

    public async Task<Result> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        Result apply = await _ledger.ApplyAsync(
            request.ProductId,
            request.WarehouseId,
            request.QuantityChange,
            request.UnitCost,
            StockMovementType.Adjust,
            request.Reason,
            referenceCode: null,
            cancellationToken);

        if (apply.IsFailure)
        {
            return apply;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
