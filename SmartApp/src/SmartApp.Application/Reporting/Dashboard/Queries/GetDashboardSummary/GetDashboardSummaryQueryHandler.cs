using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Reporting.Dashboard.Dtos;
using SmartApp.Domain.Purchasing.Enums;
using SmartApp.Domain.Sales.Enums;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Reporting.Dashboard.Queries.GetDashboardSummary;

/// <summary>
/// Computes the dashboard summary over a date range (all tenant-filtered):
/// <list type="bullet">
///   <item><description>Sales/purchase totals = sum of non-cancelled invoice grand totals in range.</description></item>
///   <item><description>Gross profit = Σ (UnitPrice − UnitCost) × (Quantity − ReturnedQty) over non-cancelled sales lines.</description></item>
///   <item><description>Outstanding receivables/payables = Σ (GrandTotal − PaidAmount) of non-cancelled invoices.</description></item>
///   <item><description>Low-stock products = distinct products whose total on-hand is below their reorder level.</description></item>
/// </list>
/// </summary>
public sealed class GetDashboardSummaryQueryHandler
    : IRequestHandler<GetDashboardSummaryQuery, Result<DashboardSummaryDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetDashboardSummaryQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<DashboardSummaryDto>> Handle(
        GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        DateTime to = request.To ?? _clock.UtcNow;
        DateTime from = request.From ?? to.AddDays(-30);

        var salesInvoices = _db.SalesInvoices
            .Where(i => i.Status != SalesInvoiceStatus.Cancelled
                        && i.InvoiceDate >= from && i.InvoiceDate <= to);

        decimal salesTotal = await salesInvoices.SumAsync(i => (decimal?)i.GrandTotal, cancellationToken) ?? 0m;
        int salesCount = await salesInvoices.CountAsync(cancellationToken);

        var purchaseInvoices = _db.PurchaseInvoices
            .Where(i => i.Status != PurchaseInvoiceStatus.Cancelled
                        && i.InvoiceDate >= from && i.InvoiceDate <= to);

        decimal purchasesTotal = await purchaseInvoices.SumAsync(i => (decimal?)i.GrandTotal, cancellationToken) ?? 0m;
        int purchaseCount = await purchaseInvoices.CountAsync(cancellationToken);

        // Gross profit over non-cancelled sales lines in range (net of returned quantity).
        decimal grossProfit = await _db.SalesInvoiceItems
            .Where(x => x.SalesInvoice!.Status != SalesInvoiceStatus.Cancelled
                        && x.SalesInvoice!.InvoiceDate >= from && x.SalesInvoice!.InvoiceDate <= to)
            .Select(x => (x.UnitPrice - x.UnitCost) * (x.Quantity - x.ReturnedQty))
            .SumAsync(v => (decimal?)v, cancellationToken) ?? 0m;

        // Outstanding balances (not date-bounded — these are current standings).
        decimal receivables = await _db.SalesInvoices
            .Where(i => i.Status != SalesInvoiceStatus.Cancelled)
            .Select(i => i.GrandTotal - i.PaidAmount)
            .SumAsync(v => (decimal?)v, cancellationToken) ?? 0m;

        decimal payables = await _db.PurchaseInvoices
            .Where(i => i.Status != PurchaseInvoiceStatus.Cancelled)
            .Select(i => i.GrandTotal - i.PaidAmount)
            .SumAsync(v => (decimal?)v, cancellationToken) ?? 0m;

        // Low stock: products whose total on-hand (across warehouses) is below their reorder level.
        var onHandByProduct = await _db.Stocks
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(s => s.QtyOnHand) })
            .ToListAsync(cancellationToken);

        var reorderByProduct = await _db.Products
            .Where(p => p.TrackStock)
            .Select(p => new { p.Id, p.ReorderLevel })
            .ToListAsync(cancellationToken);

        var onHandMap = onHandByProduct.ToDictionary(x => x.ProductId, x => x.Qty);
        int lowStock = reorderByProduct.Count(p =>
            p.ReorderLevel > 0 && (onHandMap.TryGetValue(p.Id, out decimal qty) ? qty : 0m) < p.ReorderLevel);

        return Result.Success(new DashboardSummaryDto(
            from, to, salesTotal, purchasesTotal, grossProfit,
            receivables, payables, salesCount, purchaseCount, lowStock));
    }
}
