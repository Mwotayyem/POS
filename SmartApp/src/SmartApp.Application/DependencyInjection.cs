using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace SmartApp.Application;

/// <summary>
/// Composition entry point for the Application layer.
/// Registers CQRS (MediatR), validators (FluentValidation), and pipeline behaviors.
/// Phase 1: structure only — handlers/validators are added per feature in later phases.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        Assembly assembly = typeof(DependencyInjection).Assembly;

        // CQRS — MediatR scans this assembly for IRequestHandler implementations.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

        // Validation — FluentValidation scans this assembly for AbstractValidator implementations.
        services.AddValidatorsFromAssembly(assembly);

        // NOTE (Phase 3): AutoMapper profiles will be registered here once a mapping
        // library/version is finalized. Deferred in Phase 1 — no mapping code exists yet.
        // services.AddAutoMapper(assembly);

        // NOTE (Phase 2+): MediatR pipeline behaviors are registered here in order:
        // Logging -> Validation -> TenantGuard -> Transaction.
        // See SmartApp-Architecture/02-Solution-Architecture.md §4.
        // services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        // services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
