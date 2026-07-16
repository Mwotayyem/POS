using MediatR;
using SmartApp.Application.Reporting.Reports.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Reporting.Reports.Queries.GetProductSalesReport;

/// <summary>
/// Product sales performance over a date range (defaults: last 30 days): quantity sold, revenue,
/// cost, and profit per product, ordered by profit descending. Cancelled invoices excluded; returned
/// quantities netted out. UTC bounds.
/// </summary>
public sealed record GetProductSalesReportQuery(DateTime? From = null, DateTime? To = null)
    : IRequest<Result<IReadOnlyList<ProductSalesDto>>>;
