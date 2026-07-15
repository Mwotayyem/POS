using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.TenantSettings.Commands.UpdateTenantSettings;

/// <summary>
/// Updates (or creates on first use) the current tenant's settings. TenantId is stamped server-side.
/// </summary>
public sealed record UpdateTenantSettingsCommand(
    string Currency,
    string TimeZone,
    decimal DefaultTaxRate,
    string Locale,
    string? ThemeJson) : IRequest<Result>;
