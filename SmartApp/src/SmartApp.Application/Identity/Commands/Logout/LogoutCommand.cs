using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Identity.Commands.Logout;

/// <summary>Revokes the presented refresh token. See SmartApp-Architecture/12-API-Architecture.md §6.1.</summary>
public sealed record LogoutCommand(string RefreshToken) : IRequest<Result>;
