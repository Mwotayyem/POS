using MediatR;
using SmartApp.Application.Reporting.Reports.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Reporting.Reports.Queries.GetSalesReport;

/// <summary>
/// Sales totals grouped by day over a date range (defaults: last 30 days). UTC bounds. Cancelled
/// invoices are excluded.
/// </summary>
public sealed record GetSalesReportQuery(DateTime? From = null, DateTime? To = null)
    : IRequest<Result<IReadOnlyList<SalesByDayDto>>>;
