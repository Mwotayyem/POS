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

    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(ConnectionStringName);

        // Interceptor is resolved from DI so it can consume ICurrentUserService / IDateTimeProvider.
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));

            // Wire the audit/soft-delete interceptor into the context.
            options.AddInterceptors(
                serviceProvider.GetRequiredService<ISaveChangesInterceptor>());
        });

        // Application talks to the context only through the abstraction (Dependency Inversion).
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // NOTE (later phases): repositories, unit of work, and seeders are registered here.
        // services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        // services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
