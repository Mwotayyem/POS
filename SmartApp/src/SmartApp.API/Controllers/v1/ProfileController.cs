using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartApp.Application.Profile.Commands.ChangeMyPassword;
using SmartApp.Application.Profile.Commands.UpdateMyProfile;
using SmartApp.Application.Profile.Queries.GetMyProfile;

namespace SmartApp.API.Controllers.v1;

/// <summary>
/// The authenticated user's own profile. Requires authentication but no specific permission — every
/// user may read and edit their own profile and change their own password. The user id is always
/// taken from the token, never the request body. See SmartApp-Architecture/11-Security-Architecture.md.
/// </summary>
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/profile")]
public sealed class ProfileController : ApiControllerBase
{
    /// <summary>Returns the caller's profile (identity, roles, effective permissions).</summary>
    [HttpGet]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMyProfileQuery(), cancellationToken);
        return ToResponse(result);
    }

    /// <summary>Updates the caller's own name and phone.</summary>
    [HttpPut]
    public async Task<IActionResult> UpdateMyProfile(
        [FromBody] UpdateMyProfileRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new UpdateMyProfileCommand(request.FullName, request.Phone), cancellationToken);
        return ToResponse(result);
    }

    /// <summary>Changes the caller's password (requires the current password).</summary>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangeMyPassword(
        [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new ChangeMyPasswordCommand(request.CurrentPassword, request.NewPassword), cancellationToken);
        return ToResponse(result);
    }
}

/// <summary>Update-profile request body.</summary>
public sealed record UpdateMyProfileRequest(string FullName, string? Phone);

/// <summary>Change-password request body.</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
