using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Administration.TenantSettings.Dtos;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Shared.Results;
using Entity = SmartApp.Domain.Tenancy.TenantSetting;

namespace SmartApp.Application.Administration.TenantSettings.Queries.GetTenantSettings;

/// <summary>
/// Loads the current tenant's settings row (tenant-filtered). Falls back to platform defaults when
/// the tenant has no settings row yet — read never mutates.
/// </summary>
public sealed class GetTenantSettingsQueryHandler
    : IRequestHandler<GetTenantSettingsQuery, Result<TenantSettingsDto>>
{
    private readonly IApplicationDbContext _db;

    public GetTenantSettingsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<TenantSettingsDto>> Handle(
        GetTenantSettingsQuery request, CancellationToken cancellationToken)
    {
        Entity? settings = await _db.TenantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        // Defaults mirror the TenantSetting property initializers.
        settings ??= new Entity();

        return Result.Success(new TenantSettingsDto(
            settings.Currency,
            settings.TimeZone,
            settings.DefaultTaxRate,
            settings.Locale,
            settings.ThemeJson));
    }
}
