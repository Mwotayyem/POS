using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Profile.Commands.UpdateMyProfile;

/// <summary>
/// Updates the authenticated user's own display name and phone. The user id comes from the principal,
/// never the request. Email and roles are not self-editable.
/// </summary>
public sealed record UpdateMyProfileCommand(string FullName, string? Phone) : IRequest<Result>;
