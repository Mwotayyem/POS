using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartApp.API.Authorization;
using SmartApp.Infrastructure.Identity;

namespace SmartApp.API.Extensions;

/// <summary>
/// Configures JWT bearer authentication and permission-based authorization.
/// The token validation parameters are bound lazily from <see cref="JwtSettings"/> (via
/// <see cref="IConfigureOptions{TOptions}"/>) so they reflect the final merged configuration —
/// important for tests that override config after service registration.
/// See SmartApp-Architecture/10-Identity-RBAC.md §4, §6 and 11-Security-Architecture.md.
/// </summary>
public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Bind JwtBearerOptions from JwtSettings lazily (after all configuration sources are merged).
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((bearer, jwtSettings) =>
            {
                JwtSettings settings = jwtSettings.Value;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = settings.Issuer,
                    ValidAudience = settings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        // Permission-based authorization: dynamic policies + the requirement handler.
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization();

        return services;
    }
}
