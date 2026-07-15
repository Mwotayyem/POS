namespace SmartApp.Application.Administration.Users.Dtos;

/// <summary>
/// A user as returned by the administration API. Never exposes the password hash.
/// Roles are the display names assigned to the user. See SmartApp-Architecture/12-API-Architecture.md.
/// </summary>
public sealed record UserDto(
    long Id,
    string Email,
    string FullName,
    string? Phone,
    bool IsActive,
    DateTime? LastLoginAt,
    IReadOnlyList<RoleSummaryDto> Roles);

/// <summary>A lightweight role reference (id + name) attached to a user.</summary>
public sealed record RoleSummaryDto(long Id, string Name);
