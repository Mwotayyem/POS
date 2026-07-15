using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartApp.Application.Identity.Commands.Login;
using SmartApp.Application.Identity.Commands.Logout;
using SmartApp.Application.Identity.Commands.Refresh;

namespace SmartApp.API.Controllers.v1;

/// <summary>
/// Authentication endpoints: login, refresh (with rotation), logout (revoke).
/// All are anonymous (they establish or end a session). See SmartApp-Architecture/12-API-Architecture.md §6.1.
/// </summary>
[ApiVersion("1.0")]
[AllowAnonymous]
public sealed class AuthController : ApiControllerBase
{
    /// <summary>Authenticates by email + password and returns an access/refresh token pair.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new LoginCommand(request.Email, request.Password, HttpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);
        return ToResponse(result);
    }

    /// <summary>Exchanges a valid refresh token for a new pair (rotates the old one).</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new RefreshTokenCommand(request.RefreshToken, HttpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);
        return ToResponse(result);
    }

    /// <summary>Revokes the presented refresh token.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new LogoutCommand(request.RefreshToken), cancellationToken);
        return ToResponse(result);
    }
}

/// <summary>Login request body.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Refresh request body.</summary>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Logout request body.</summary>
public sealed record LogoutRequest(string RefreshToken);
