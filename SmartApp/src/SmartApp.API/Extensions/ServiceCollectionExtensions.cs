namespace SmartApp.API.Extensions;

/// <summary>
/// Registers presentation-layer (API) services: controllers, versioning, Swagger.
/// This is the API slice of the composition root; cross-layer wiring happens in Program.cs.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddControllers();

        services.AddApiVersioningConfig();
        services.AddSwaggerConfig();

        // NOTE (Phase 3): CORS whitelist, rate limiting, JWT authentication, and
        // permission-based authorization are configured here.
        // See SmartApp-Architecture/11-Security-Architecture.md.

        return services;
    }
}
