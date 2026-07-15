namespace SmartApp.Application.Administration.TenantSettings.Dtos;

/// <summary>
/// The current tenant's configuration (currency, timezone, default tax rate, locale, optional theme).
/// One row per tenant. See SmartApp-Architecture/06-Tables-Definitions.md §1.2.
/// </summary>
public sealed record TenantSettingsDto(
    string Currency,
    string TimeZone,
    decimal DefaultTaxRate,
    string Locale,
    string? ThemeJson);
