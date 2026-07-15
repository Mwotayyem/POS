using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Infrastructure.Identity;
using SmartApp.Infrastructure.MultiTenancy;
using SmartApp.Infrastructure.Security;
using SmartApp.Infrastructure.Services;

namespace SmartApp.Infrastructure;

/// <summary>
/// Composition entry point for the Infrastructure layer.
/// Implements Application interfaces that are NOT data access (tenant/user context, clock,
/// password hashing, JWT). Data access lives in SmartApp.Persistence.
/// Phase 4 adds the authentication foundation (password hasher + JWT service + settings binding).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Required by TenantProvider / CurrentUserService to read the authenticated principal.
        services.AddHttpContextAccessor();

        // Multi-tenancy + current-user context (scoped: per request).
        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Clock (singleton: stateless).
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        // ---- Authentication foundation (Phase 4) ----
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtService, JwtService>();

        return services;
    }
}
