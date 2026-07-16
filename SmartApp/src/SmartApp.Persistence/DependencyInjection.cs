using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Persistence.Context;
using SmartApp.Persistence.Interceptors;

namespace SmartApp.Persistence;

/// <summary>
/// Composition entry point for the Persistence layer.
/// Registers the EF Core <see cref="AppDbContext"/> (SQL Server), the audit interceptor,
/// and exposes it through <see cref="IApplicationDbContext"/>.
/// Phase 2: foundation only — no entities/DbSets yet, no seeders.
/// </summary>
public static class DependencyInjection
{
    public const string ConnectionStringName = "SmartAppDb";

    /// <summary>Configuration key selecting the database provider ("SqlServer" | "Sqlite").</summary>
    public const string ProviderConfigKey = "Database:Provider";

    /// <summary>Supported database providers.</summary>
    public enum DbProvider
    {
        /// <summary>SQL Server (default; production).</summary>
        SqlServer,

        /// <summary>SQLite local file — for development/demo without a SQL Server instance.</summary>
        Sqlite,
    }

    /// <summary>
    /// Reads the configured provider (<see cref="ProviderConfigKey"/>). Defaults to
    /// <see cref="DbProvider.SqlServer"/> when unset or unrecognized.
    /// </summary>
    public static DbProvider GetDbProvider(this IConfiguration configuration) =>
        Enum.TryParse(configuration[ProviderConfigKey], ignoreCase: true, out DbProvider provider)
            ? provider
            : DbProvider.SqlServer;

    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(ConnectionStringName);
        DbProvider provider = configuration.GetDbProvider();

        // Interceptor is resolved from DI so it can consume ICurrentUserService / IDateTimeProvider.
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            switch (provider)
            {
                case DbProvider.Sqlite:
                    // Local-file SQLite (schema created via EnsureCreated at startup — the SQL Server
                    // migrations are provider-specific and are not applied here). The DbContext already
                    // neutralizes SQL Server-only features (ROWVERSION, ISJSON, SYSUTCDATETIME) for
                    // non-SQL-Server providers, so the model maps cleanly.
                    options.UseSqlite(connectionString);
                    break;

                case DbProvider.SqlServer:
                default:
                    options.UseSqlServer(connectionString, sql =>
                        sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
                    break;
            }

            // Wire the audit/soft-delete interceptor into the context.
            options.AddInterceptors(
                serviceProvider.GetRequiredService<ISaveChangesInterceptor>());
        });

        // Application talks to the context only through the abstraction (Dependency Inversion).
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        return services;
    }
}
