using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Inventory.Enums;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Inventory.Stock.Commands.TransferStock;

/// <summary>
/// Moves stock between two warehouses as two linked ledger movements (OUT then IN) sharing a
/// reference code, committed together. The inbound uses the source's current average cost so the
/// transfer neither creates nor destroys value. The ledger rejects the OUT leg if the source lacks
/// sufficient quantity, in which case nothing is committed.
/// </summary>
public sealed class TransferStockCommandHandler : IRequestHandler<TransferStockCommand, Result>
{
    private readonly IStockLedger _ledger;
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public TransferStockCommandHandler(IStockLedger ledger, IApplicationDbContext db, IDateTimeProvider clock)
    {
        _ledger = ledger;
        _db = db;
        _clock = clock;
    }

    public async Task<Result> Handle(TransferStockCommand request, CancellationToken cancellationToken)
    {
        if (request.FromWarehouseId == request.ToWarehouseId)
        {
            return Result.Failure(Error.Validation(
                "لا يمكن التحويل إلى نفس المستودع.",
                [new FieldError("toWarehouseId", "يجب أن يختلف المستودع الهدف عن المصدر.")]));
        }

        // Capture the source's current average cost so the inbound leg preserves cost.
        var sourceStock = await _db.Stocks.FirstOrDefaultAsync(
            s => s.ProductId == request.ProductId && s.WarehouseId == request.FromWarehouseId,
            cancellationToken);
        decimal transferCost = sourceStock?.AvgCost ?? 0m;

        string reference = BuildReferenceCode();

        // OUT of the source (deducted at current average cost).
        Result outLeg = await _ledger.ApplyAsync(
            request.ProductId, request.FromWarehouseId, -request.Quantity, transferCost,
            StockMovementType.Transfer, request.Reason, reference, cancellationToken);
        if (outLeg.IsFailure)
        {
            return outLeg;
        }

        // IN to the destination at the same cost.
        Result inLeg = await _ledger.ApplyAsync(
            request.ProductId, request.ToWarehouseId, request.Quantity, transferCost,
            StockMovementType.Transfer, request.Reason, reference, cancellationToken);
        if (inLeg.IsFailure)
        {
            return inLeg;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>A short, sortable reference correlating the two transfer legs (no RNG/clock in domain).</summary>
    private string BuildReferenceCode()
    {
        DateTime now = _clock.UtcNow;
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"TRF-{now:yyyyMMddHHmmssfff}");
    }
}
