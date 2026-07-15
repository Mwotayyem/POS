namespace SmartApp.Application.Administration.Roles.Dtos;

/// <summary>
/// A role with its granted permission codes. <see cref="IsSystemRole"/> roles (e.g. Owner) ship with
/// the tenant and cannot be deleted. See SmartApp-Architecture/10-Identity-RBAC.md §3.
/// </summary>
public sealed record RoleDto(
    long Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    IReadOnlyList<string> Permissions);
