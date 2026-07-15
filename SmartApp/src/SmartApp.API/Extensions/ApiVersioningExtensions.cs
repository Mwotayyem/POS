using Asp.Versioning;

namespace SmartApp.API.Extensions;

/// <summary>
/// API versioning structure — versions are carried in the URL path (/api/v1/...)
/// per SmartApp-Architecture/12-API-Architecture.md §5.
/// </summary>
public static class ApiVersioningExtensions
{
    public static IServiceCollection AddApiVersioningConfig(this IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(options =>
            {
                // Formats the version as "'v'major[.minor]" — e.g. v1 — used by Swagger doc grouping.
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        return services;
    }
}
