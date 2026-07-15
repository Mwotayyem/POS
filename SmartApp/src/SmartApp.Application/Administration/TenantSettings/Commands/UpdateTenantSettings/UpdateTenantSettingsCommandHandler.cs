using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Shared.Results;
using Entity = SmartApp.Domain.Tenancy.TenantSetting;

namespace SmartApp.Application.Administration.TenantSettings.Commands.UpdateTenantSettings;

/// <summary>
/// Upserts the current tenant's settings: updates the existing row, or creates one on first use.
/// TenantId is auto-stamped on insert (TenantSetting is a BaseEntity); the global filter ensures the
/// loaded row belongs to the caller's tenant.
/// </summary>
public sealed class UpdateTenantSettingsCommandHandler
    : IRequestHandler<UpdateTenantSettingsCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public UpdateTenantSettingsCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(UpdateTenantSettingsCommand request, CancellationToken cancellationToken)
    {
        Entity? settings = await _db.TenantSettings.FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            settings = new Entity();
            _db.TenantSettings.Add(settings);
        }

        settings.Currency = request.Currency.Trim();
        settings.TimeZone = request.TimeZone.Trim();
        settings.DefaultTaxRate = request.DefaultTaxRate;
        settings.Locale = request.Locale.Trim();
        settings.ThemeJson = string.IsNullOrWhiteSpace(request.ThemeJson) ? null : request.ThemeJson;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
