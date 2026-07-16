using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Reporting.Reports.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Reporting.Reports.Queries.GetLowStockReport;

/// <summary>
/// Computes on-hand totals per product and returns those at or below their reorder level. Both the
/// products and stock are tenant-filtered.
/// </summary>
public sealed class GetLowStockReportQueryHandler
    : IRequestHandler<GetLowStockReportQuery, Result<IReadOnlyList<LowStockItemDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetLowStockReportQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<LowStockItemDto>>> Handle(
        GetLowStockReportQuery request, CancellationToken cancellationToken)
    {
        var products = await _db.Products
            .Where(p => p.TrackStock && p.ReorderLevel > 0)
            .Select(p => new { p.Id, p.Name, p.Sku, p.ReorderLevel })
            .ToListAsync(cancellationToken);

        var onHand = await _db.Stocks
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(s => s.QtyOnHand) })
            .ToListAsync(cancellationToken);

        var onHandMap = onHand.ToDictionary(x => x.ProductId, x => x.Qty);

        List<LowStockItemDto> low = products
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                p.ReorderLevel,
                Qty = onHandMap.TryGetValue(p.Id, out decimal q) ? q : 0m,
            })
            .Where(p => p.Qty <= p.ReorderLevel)
            .OrderBy(p => p.Qty)
            .Select(p => new LowStockItemDto(p.Id, p.Name, p.Sku, p.Qty, p.ReorderLevel))
            .ToList();

        return Result.Success<IReadOnlyList<LowStockItemDto>>(low);
    }
}
