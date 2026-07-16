using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SmartApp.API.HealthChecks;

namespace SmartApp.API.Extensions;

/// <summary>
/// Production-hardening services (Phase 12): a database-backed health check, a configurable CORS
/// whitelist, and a global fixed-window rate limiter. All are configuration-driven with safe defaults
/// so non-production/test environments are unaffected.
/// See SmartApp-Architecture/11-Security-Architecture.md.
/// </summary>
public static class ProductionExtensions
{
    public const string CorsPolicyName = "SmartAppCors";
    public const string RateLimiterPolicyName = "SmartAppFixedWindow";

    /// <summary>
    /// Registers health checks, CORS (origins from <c>Cors:AllowedOrigins</c>), and a global rate
    /// limiter (tunable via <c>RateLimiting:*</c>; enabled by default, disable with
    /// <c>RateLimiting:Enabled=false</c>).
    /// </summary>
    public static IServiceCollection AddProductionServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database");

        string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? [];

        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            }
            else
            {
                // No whitelist configured → same-origin only (no cross-origin allowed).
                policy.SetIsOriginAllowed(_ => false);
            }
        }));

        int permitLimit = configuration.GetValue("RateLimiting:PermitLimit", 100);
        int windowSeconds = configuration.GetValue("RateLimiting:WindowSeconds", 60);
        int queueLimit = configuration.GetValue("RateLimiting:QueueLimit", 0);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter(RateLimiterPolicyName, limiter =>
            {
                limiter.PermitLimit = permitLimit;
                limiter.Window = TimeSpan.FromSeconds(windowSeconds);
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.QueueLimit = queueLimit;
            });
        });

        return services;
    }

    /// <summary>
    /// True when the rate limiter should be applied to the request pipeline. Opt-in via
    /// <c>RateLimiting:Enabled=true</c> so tests and local development are not throttled.
    /// </summary>
    public static bool IsRateLimitingEnabled(this IConfiguration configuration)
        => configuration.GetValue("RateLimiting:Enabled", false);
}
