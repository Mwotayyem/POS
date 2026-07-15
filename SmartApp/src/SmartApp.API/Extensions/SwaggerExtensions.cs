using Microsoft.OpenApi.Models;

namespace SmartApp.API.Extensions;

/// <summary>
/// Swagger / OpenAPI configuration. Includes a JWT Bearer security definition so that,
/// once authentication is implemented (Phase 3), protected endpoints can be exercised.
/// Swagger UI is only enabled in non-production environments (see Program.cs) per
/// SmartApp-Architecture/11-Security-Architecture.md (A05).
/// </summary>
public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerConfig(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "SmartApp API",
                Version = "v1",
                Description = "SmartApp — Business Management Platform (Multi-Tenant). Phase 1 skeleton.",
            });

            var jwtScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "أدخل التوكن هكذا: Bearer {token}",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            };

            options.AddSecurityDefinition("Bearer", jwtScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { jwtScheme, Array.Empty<string>() },
            });
        });

        return services;
    }
}
