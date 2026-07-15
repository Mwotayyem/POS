using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SmartApp.Persistence;

/// <summary>
/// Composition entry point for the Persistence layer.
/// Registers the EF Core DbContext, repositories, interceptors, and seeders.
/// Phase 1: structure only — AppDbContext is introduced in Phase 2 (Tenancy + Isolation).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // NOTE (Phase 2): register AppDbContext with SQL Server + interceptors.
        // string? connectionString = configuration.GetConnectionString("SmartAppDb");
        // services.AddDbContext<AppDbContext>(options =>
        //     options.UseSqlServer(connectionString));
        // services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // NOTE (Phase 2): repositories, unit of work, interceptors.
        // services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        // services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
