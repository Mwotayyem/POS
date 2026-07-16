using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SmartApp.API.Authorization;
using SmartApp.Application.Reporting.Dashboard.Queries.GetDashboardSummary;
using SmartApp.Shared.Constants;

namespace SmartApp.API.Controllers.v1;

/// <summary>
/// Business dashboard: a tenant-scoped activity summary over a date range (sales, purchases, profit,
/// receivables/payables, low-stock count). Read-only, guarded by the reports permission.
/// </summary>
[ApiVersion("1.0")]
public sealed class DashboardController : ApiControllerBase
{
    /// <summary>Returns the dashboard summary for the given range (defaults to the last 30 days).</summary>
    [HttpGet("summary")]
    [HasPermission(Permissions.Reports.View)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetDashboardSummaryQuery(from, to), cancellationToken));
}
