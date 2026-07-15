using MediatR;
using SmartApp.Application.Administration.TenantSettings.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.TenantSettings.Queries.GetTenantSettings;

/// <summary>
/// Returns the current tenant's settings. If none exist yet, the platform defaults are returned
/// (without persisting) so the client always has a value to render.
/// </summary>
public sealed record GetTenantSettingsQuery : IRequest<Result<TenantSettingsDto>>;
