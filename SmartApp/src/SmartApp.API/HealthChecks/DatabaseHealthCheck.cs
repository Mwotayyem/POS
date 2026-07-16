using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SmartApp.Persistence.Context;

namespace SmartApp.API.HealthChecks;

/// <summary>
/// Reports the API as healthy only when the database is reachable. Used by the <c>/health</c>
/// endpoint for load-balancer / orchestrator probes. Uses <c>CanConnectAsync</c> so it is cheap and
/// does not run any query. See SmartApp-Architecture/11-Security-Architecture.md.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;

    public DatabaseHealthCheck(AppDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            bool canConnect = await _db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Database reachable.")
                : HealthCheckResult.Unhealthy("Database not reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check failed.", ex);
        }
    }
}
