using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Inventory.Stock.Dtos;
using SmartApp.Shared.Results;
using StockEntity = SmartApp.Domain.Inventory.Stock;

namespace SmartApp.Application.Inventory.Stock.Queries.GetStockBalances;

/// <summary>Loads the current tenant's stock balances (tenant-filtered), with product/warehouse names.</summary>
public sealed class GetStockBalancesQueryHandler
    : IRequestHandler<GetStockBalancesQuery, Result<IReadOnlyList<StockBalanceDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetStockBalancesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<StockBalanceDto>>> Handle(
        GetStockBalancesQuery request, CancellationToken cancellationToken)
    {
        IQueryable<StockEntity> query = _db.Stocks;

        if (request.WarehouseId is long warehouseId)
        {
            query = query.Where(s => s.WarehouseId == warehouseId);
        }

        if (request.ProductId is long productId)
        {
            query = query.Where(s => s.ProductId == productId);
        }

        List<StockBalanceDto> balances = await query
            .OrderBy(s => s.ProductId)
            .Select(s => new StockBalanceDto(
                s.Id,
                s.ProductId,
                s.Product!.Name,
                s.WarehouseId,
                s.Warehouse!.Name,
                s.QtyOnHand,
                s.AvgCost))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<StockBalanceDto>>(balances);
    }
}
