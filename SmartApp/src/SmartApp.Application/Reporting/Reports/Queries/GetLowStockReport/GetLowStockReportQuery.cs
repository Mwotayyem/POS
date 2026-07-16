using MediatR;
using SmartApp.Application.Reporting.Reports.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Reporting.Reports.Queries.GetLowStockReport;

/// <summary>
/// Lists stock-tracked products whose total on-hand quantity (across warehouses) is at or below their
/// reorder level (reorder level must be &gt; 0 to be considered).
/// </summary>
public sealed record GetLowStockReportQuery : IRequest<Result<IReadOnlyList<LowStockItemDto>>>;
