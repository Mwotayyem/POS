using MediatR;
using SmartApp.Application.Reporting.Dashboard.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Reporting.Dashboard.Queries.GetDashboardSummary;

/// <summary>
/// Returns the dashboard summary for a date range. If not provided, <see cref="From"/> defaults to
/// 30 days before <see cref="To"/>, and <see cref="To"/> defaults to now. Bounds are treated as UTC.
/// </summary>
public sealed record GetDashboardSummaryQuery(DateTime? From = null, DateTime? To = null)
    : IRequest<Result<DashboardSummaryDto>>;
