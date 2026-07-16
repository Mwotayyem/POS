using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SmartApp.API.Authorization;
using SmartApp.Application.Reporting.Reports.Queries.GetLowStockReport;
using SmartApp.Application.Reporting.Reports.Queries.GetProductSalesReport;
using SmartApp.Application.Reporting.Reports.Queries.GetSalesReport;
using SmartApp.Shared.Constants;

namespace SmartApp.API.Controllers.v1;

/// <summary>
/// Business reports (read-only, tenant-scoped): sales by day, low stock, and product sales
/// performance. Guarded by the reports permission.
/// </summary>
[ApiVersion("1.0")]
public sealed class ReportsController : ApiControllerBase
{
    /// <summary>Sales totals grouped by day over a date range (defaults to the last 30 days).</summary>
    [HttpGet("sales")]
    [HasPermission(Permissions.Reports.View)]
    public async Task<IActionResult> GetSalesReport(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetSalesReportQuery(from, to), cancellationToken));

    /// <summary>Products at or below their reorder level.</summary>
    [HttpGet("low-stock")]
    [HasPermission(Permissions.Reports.View)]
    public async Task<IActionResult> GetLowStock(CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetLowStockReportQuery(), cancellationToken));

    /// <summary>Product sales performance (qty/revenue/cost/profit) over a range, ordered by profit.</summary>
    [HttpGet("products")]
    [HasPermission(Permissions.Reports.View)]
    public async Task<IActionResult> GetProductSales(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetProductSalesReportQuery(from, to), cancellationToken));
}
