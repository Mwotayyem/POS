using MediatR;
using SmartApp.Application.Profile.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Profile.Queries.GetMyProfile;

/// <summary>Returns the currently authenticated user's own profile (identity, roles, permissions).</summary>
public sealed record GetMyProfileQuery : IRequest<Result<ProfileDto>>;
