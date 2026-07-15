namespace SmartApp.Application.Administration.Permissions.Dtos;

/// <summary>
/// A permission as exposed by the API: its code, owning module, and display name. Read-only — the
/// catalog is fixed and seeded, never created at runtime. See SmartApp-Architecture/10-Identity-RBAC.md §2.
/// </summary>
public sealed record PermissionDto(string Code, string Module, string DisplayName);
