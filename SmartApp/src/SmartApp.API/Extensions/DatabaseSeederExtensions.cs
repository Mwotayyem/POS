using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Persistence;
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
    /// When the configured provider is SQLite (development/demo), creates the database file and schema
    /// if they do not yet exist (<c>EnsureCreated</c>). No-op for SQL Server, whose schema is managed by
    /// EF Core migrations (<c>dotnet ef database update</c>). Non-fatal: failures are logged only.
    /// Must run before seeding so the tables exist.
    /// </summary>
    public static async Task EnsureSqliteDatabaseAsync(
        this IHost app, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (configuration.GetDbProvider() != DependencyInjection.DbProvider.Sqlite)
        {
            return;
        }

        ILogger logger = app.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DatabaseSeederExtensions));

        try
        {
            using IServiceScope scope = app.Services.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            bool created = await db.Database.EnsureCreatedAsync(cancellationToken);
            LogSqliteReady(logger, created);
        }
        catch (Exception ex)
        {
            LogSqliteFailed(logger, ex);
        }
    }

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

    /// <summary>
    /// Development-only: seeds a default Owner login (see <see cref="DevDataSeeder"/>) so the SPA can be
    /// used immediately. Opt-in via configuration <c>Seed:DevData=true</c> (defaulted on in
    /// appsettings.Development.json). No-op if any user already exists. Non-fatal: an unavailable
    /// database logs a warning and does not block startup. Credentials come from
    /// <c>Seed:OwnerEmail</c> / <c>Seed:OwnerPassword</c> (with safe local defaults).
    /// </summary>
    public static async Task SeedDevDataAsync(
        this IHost app, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue("Seed:DevData", false))
        {
            return;
        }

        ILogger logger = app.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DatabaseSeederExtensions));

        string ownerEmail = configuration.GetValue("Seed:OwnerEmail", "admin@smartapp.local")!;
        string ownerPassword = configuration.GetValue("Seed:OwnerPassword", "Admin@123456")!;
        string tenantName = configuration.GetValue("Seed:TenantName", "Demo Company")!;
        string tenantCode = configuration.GetValue("Seed:TenantCode", "DEMO")!;

        try
        {
            using IServiceScope scope = app.Services.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            DevDataSeeder.SeedOutcome outcome = await DevDataSeeder.SeedAsync(
                db, hasher, ownerEmail, ownerPassword, tenantName, tenantCode, cancellationToken);

            if (outcome == DevDataSeeder.SeedOutcome.Created)
            {
                LogDevSeeded(logger, ownerEmail);
            }
            else
            {
                LogDevSkippedExists(logger);
            }
        }
        catch (Exception ex)
        {
            LogDevSkippedError(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SQLite database ready (created this run: {Created}).")]
    private static partial void LogSqliteReady(ILogger logger, bool created);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "SQLite database initialization failed.")]
    private static partial void LogSqliteFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Permission catalog seeding completed ({Inserted} new permissions).")]
    private static partial void LogSeeded(ILogger logger, int inserted);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Permission catalog seeding was skipped (database unavailable).")]
    private static partial void LogSkipped(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Dev data seeded: created default Owner login '{Email}'.")]
    private static partial void LogDevSeeded(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Dev data seeding skipped: users already exist.")]
    private static partial void LogDevSkippedExists(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Dev data seeding was skipped (database unavailable).")]
    private static partial void LogDevSkippedError(ILogger logger, Exception exception);
}
