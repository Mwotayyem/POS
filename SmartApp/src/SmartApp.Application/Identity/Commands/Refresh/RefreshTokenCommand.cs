using MediatR;
using SmartApp.Application.Identity.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Identity.Commands.Refresh;

/// <summary>
/// Exchanges a valid refresh token for a new access/refresh pair (with rotation + reuse detection).
/// See SmartApp-Architecture/10-Identity-RBAC.md §5.
/// </summary>
public sealed record RefreshTokenCommand(string RefreshToken, string? CreatedByIp)
    : IRequest<Result<AuthTokensDto>>;
