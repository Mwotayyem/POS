using Microsoft.AspNetCore.Mvc;

namespace SmartApp.API.Extensions;

/// <summary>
/// Registers presentation-layer (API) services: controllers, versioning, Swagger, and
/// JWT authentication + permission-based authorization.
/// This is the API slice of the composition root; cross-layer wiring happens in Program.cs.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();

        // Suppress the automatic model-state 400 so validation flows through the MediatR
        // ValidationBehavior and returns the unified error envelope (12-API-Architecture.md §2, §4).
        services.Configure<ApiBehaviorOptions>(options =>
            options.SuppressModelStateInvalidFilter = true);

        services.AddApiVersioningConfig();
        services.AddSwaggerConfig();
        services.AddJwtAuthentication();

        // Production hardening (Phase 12): health checks, CORS whitelist, rate limiting.
        // See SmartApp-Architecture/11-Security-Architecture.md.
        services.AddProductionServices(configuration);

        return services;
    }
}
