using MediatR;
using SmartApp.Application.Identity.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Identity.Commands.Login;

/// <summary>
/// Authenticates a user by email + password and issues an access/refresh token pair.
/// See SmartApp-Architecture/12-API-Architecture.md §6.1 and 10-Identity-RBAC.md §4.
/// </summary>
public sealed record LoginCommand(string Email, string Password, string? CreatedByIp)
    : IRequest<Result<AuthTokensDto>>;
