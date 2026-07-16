using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SmartApp.Application.Common.Behaviors;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Inventory.Services;

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

        // MediatR pipeline behaviors (order matters). Validation runs before every handler.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Inventory: the stock ledger (WAC + append-only movements) is the single entry point for
        // stock changes, shared by adjustments/transfers now and Sales/Purchases later.
        services.AddScoped<IStockLedger, StockLedger>();

        // NOTE (later): AutoMapper profiles registered here once a mapping library is finalized.

        return services;
    }
}
