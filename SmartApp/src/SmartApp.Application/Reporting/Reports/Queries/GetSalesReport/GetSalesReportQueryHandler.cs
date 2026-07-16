using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Reporting.Reports.Dtos;
using SmartApp.Domain.Sales.Enums;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Reporting.Reports.Queries.GetSalesReport;

/// <summary>
/// Loads non-cancelled sales invoices in range (tenant-filtered), then groups them by calendar day
/// in memory (the range bounds the row count). Days with no sales are omitted.
/// </summary>
public sealed class GetSalesReportQueryHandler
    : IRequestHandler<GetSalesReportQuery, Result<IReadOnlyList<SalesByDayDto>>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetSalesReportQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<SalesByDayDto>>> Handle(
        GetSalesReportQuery request, CancellationToken cancellationToken)
    {
        DateTime to = request.To ?? _clock.UtcNow;
        DateTime from = request.From ?? to.AddDays(-30);

        var rows = await _db.SalesInvoices
            .Where(i => i.Status != SalesInvoiceStatus.Cancelled
                        && i.InvoiceDate >= from && i.InvoiceDate <= to)
            .Select(i => new { i.InvoiceDate, i.GrandTotal })
            .ToListAsync(cancellationToken);

        List<SalesByDayDto> report = rows
            .GroupBy(r => DateOnly.FromDateTime(r.InvoiceDate))
            .OrderBy(g => g.Key)
            .Select(g => new SalesByDayDto(g.Key, g.Sum(r => r.GrandTotal), g.Count()))
            .ToList();

        return Result.Success<IReadOnlyList<SalesByDayDto>>(report);
    }
}
