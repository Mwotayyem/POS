using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SmartApp.Infrastructure;

/// <summary>
/// Composition entry point for the Infrastructure layer.
/// Implements Application interfaces that are NOT data access (JWT, TenantProvider,
/// CurrentUser, DateTime, Email, Caching). Data access lives in SmartApp.Persistence.
/// Phase 1: structure only — concrete services are added in later phases.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Required by TenantProvider / CurrentUserService to read the authenticated principal.
        services.AddHttpContextAccessor();

        // NOTE (Phase 2): multi-tenancy services.
        // services.AddScoped<ITenantProvider, TenantProvider>();
        // services.AddScoped<ICurrentUserService, CurrentUserService>();

        // NOTE (Phase 3): identity/security services.
        // services.AddScoped<IJwtService, JwtService>();

        // NOTE (Phase 2+): common services.
        // services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        return services;
    }
}
