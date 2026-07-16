using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Reporting.Reports.Dtos;
using SmartApp.Domain.Sales.Enums;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Reporting.Reports.Queries.GetProductSalesReport;

/// <summary>
/// Aggregates non-cancelled sales invoice lines in range by product (tenant-filtered): net quantity
/// (Quantity − ReturnedQty), revenue (net qty × UnitPrice), cost (net qty × UnitCost), and profit.
/// </summary>
public sealed class GetProductSalesReportQueryHandler
    : IRequestHandler<GetProductSalesReportQuery, Result<IReadOnlyList<ProductSalesDto>>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetProductSalesReportQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<ProductSalesDto>>> Handle(
        GetProductSalesReportQuery request, CancellationToken cancellationToken)
    {
        DateTime to = request.To ?? _clock.UtcNow;
        DateTime from = request.From ?? to.AddDays(-30);

        var lines = await _db.SalesInvoiceItems
            .Where(x => x.SalesInvoice!.Status != SalesInvoiceStatus.Cancelled
                        && x.SalesInvoice!.InvoiceDate >= from && x.SalesInvoice!.InvoiceDate <= to)
            .Select(x => new
            {
                x.ProductId,
                ProductName = x.Product!.Name,
                x.Product!.Sku,
                NetQty = x.Quantity - x.ReturnedQty,
                x.UnitPrice,
                x.UnitCost,
            })
            .ToListAsync(cancellationToken);

        List<ProductSalesDto> report = lines
            .GroupBy(l => new { l.ProductId, l.ProductName, l.Sku })
            .Select(g =>
            {
                decimal qty = g.Sum(l => l.NetQty);
                decimal revenue = g.Sum(l => l.NetQty * l.UnitPrice);
                decimal cost = g.Sum(l => l.NetQty * l.UnitCost);
                return new ProductSalesDto(
                    g.Key.ProductId, g.Key.ProductName, g.Key.Sku, qty, revenue, cost, revenue - cost);
            })
            .OrderByDescending(p => p.Profit)
            .ToList();

        return Result.Success<IReadOnlyList<ProductSalesDto>>(report);
    }
}
