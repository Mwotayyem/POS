using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Inventory.Stock.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Inventory.Stock.Queries.GetStockMovements;

/// <summary>Loads a product's movement history (tenant-filtered), newest first.</summary>
public sealed class GetStockMovementsQueryHandler
    : IRequestHandler<GetStockMovementsQuery, Result<IReadOnlyList<StockMovementDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetStockMovementsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<StockMovementDto>>> Handle(
        GetStockMovementsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.StockMovements.Where(m => m.ProductId == request.ProductId);

        if (request.WarehouseId is long warehouseId)
        {
            query = query.Where(m => m.WarehouseId == warehouseId);
        }

        List<StockMovementDto> movements = await query
            .OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.Id)
            .Select(m => new StockMovementDto(
                m.Id,
                m.ProductId,
                m.WarehouseId,
                (byte)m.MovementType,
                m.QuantityChange,
                m.UnitCost,
                m.ResultingQty,
                m.ResultingAvgCost,
                m.OccurredAt,
                m.Reason,
                m.ReferenceCode))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<StockMovementDto>>(movements);
    }
}
