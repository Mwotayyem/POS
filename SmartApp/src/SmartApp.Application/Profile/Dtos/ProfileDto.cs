namespace SmartApp.Application.Profile.Dtos;

/// <summary>
/// The authenticated user's own profile: identity fields plus their role names and the flattened set
/// of effective permission codes (union across roles). Never exposes the password hash.
/// </summary>
public sealed record ProfileDto(
    long Id,
    string Email,
    string FullName,
    string? Phone,
    bool IsSystemOwner,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
