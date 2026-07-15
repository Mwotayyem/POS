using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SmartApp.API.Authorization;
using SmartApp.Application.Administration.TenantSettings.Commands.UpdateTenantSettings;
using SmartApp.Application.Administration.TenantSettings.Queries.GetTenantSettings;
using SmartApp.Shared.Constants;

namespace SmartApp.API.Controllers.v1;

/// <summary>
/// The current tenant's settings (currency, timezone, tax, locale, theme). One row per tenant;
/// reading returns defaults when unset, updating upserts. See SmartApp-Architecture/06-Tables-Definitions.md §1.2.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tenant/settings")]
public sealed class TenantSettingsController : ApiControllerBase
{
    /// <summary>Returns the current tenant's settings.</summary>
    [HttpGet]
    [HasPermission(Permissions.Settings.View)]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetTenantSettingsQuery(), cancellationToken);
        return ToResponse(result);
    }

    /// <summary>Updates (or creates on first use) the current tenant's settings.</summary>
    [HttpPut]
    [HasPermission(Permissions.Settings.Manage)]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] UpdateTenantSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new UpdateTenantSettingsCommand(
                request.Currency, request.TimeZone, request.DefaultTaxRate, request.Locale, request.ThemeJson),
            cancellationToken);
        return ToResponse(result);
    }
}

/// <summary>Update-tenant-settings request body.</summary>
public sealed record UpdateTenantSettingsRequest(
    string Currency, string TimeZone, decimal DefaultTaxRate, string Locale, string? ThemeJson);
