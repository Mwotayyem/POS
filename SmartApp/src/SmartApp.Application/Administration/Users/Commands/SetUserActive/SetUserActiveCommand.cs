using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Users.Commands.SetUserActive;

/// <summary>
/// Activates or deactivates a tenant user. Deactivating also revokes the user's active refresh
/// tokens so an existing session cannot be silently extended. Backs both the
/// <c>activate</c> and <c>deactivate</c> endpoints.
/// </summary>
public sealed record SetUserActiveCommand(long UserId, bool IsActive) : IRequest<Result>;
