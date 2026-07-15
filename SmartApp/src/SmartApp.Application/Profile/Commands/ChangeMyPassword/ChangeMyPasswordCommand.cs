using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Profile.Commands.ChangeMyPassword;

/// <summary>
/// Changes the authenticated user's password. Requires the current password. On success, all of the
/// user's active refresh tokens are revoked so other sessions must re-authenticate.
/// </summary>
public sealed record ChangeMyPasswordCommand(
    string CurrentPassword,
    string NewPassword) : IRequest<Result>;
