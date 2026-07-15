using SmartApp.Persistence.Context;
using SmartApp.Persistence.Seeding;

namespace SmartApp.API.Extensions;

/// <summary>
/// Startup hook that seeds the global permission catalog into the database. Runs once at boot,
/// after the DI container is built. Idempotent — safe on every start. Non-fatal: a database that is
/// not yet reachable/migrated logs a warning and does not prevent startup. Tenant-specific seeding
/// (Owner role) happens per-tenant at provisioning time, not here.
/// See SmartApp-Architecture/10-Identity-RBAC.md §2.
/// </summary>
public static partial class DatabaseSeederExtensions
{
    /// <summary>
    /// Seeds the permission catalog, swallowing (and logging) any failure so an unavailable database
    /// cannot block application startup.
    /// </summary>
    public static async Task SeedPermissionCatalogAsync(
        this IHost app, CancellationToken cancellationToken = default)
    {
        ILogger logger = app.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DatabaseSeederExtensions));

        try
        {
            using IServiceScope scope = app.Services.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            int inserted = await PermissionSeeder.SeedAsync(db, cancellationToken);
            LogSeeded(logger, inserted);
        }
        catch (Exception ex)
        {
            LogSkipped(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Permission catalog seeding completed ({Inserted} new permissions).")]
    private static partial void LogSeeded(ILogger logger, int inserted);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Permission catalog seeding was skipped (database unavailable).")]
    private static partial void LogSkipped(ILogger logger, Exception exception);
}
