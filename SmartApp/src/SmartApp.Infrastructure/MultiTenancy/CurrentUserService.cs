using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SmartApp.Application.Common.Interfaces;

namespace SmartApp.Infrastructure.MultiTenancy;

/// <summary>
/// Reads the current user's id from the authenticated principal on the HTTP request.
/// Phase 2 structure — the JWT that carries these claims is issued in Phase 3, so today this
/// yields <c>null</c> for unauthenticated requests. Used only to populate audit fields.
/// See SmartApp-Architecture/03-Project-Structure.md §4.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long? UserId
    {
        get
        {
            ClaimsPrincipal? user = _httpContextAccessor.HttpContext?.User;
            string? value = user?.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(value, out long id) ? id : null;
        }
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
