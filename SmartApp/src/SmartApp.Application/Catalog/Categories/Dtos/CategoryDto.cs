namespace SmartApp.Application.Catalog.Categories.Dtos;

/// <summary>
/// A category as returned by the API. <see cref="ParentId"/> models the tree.
/// See SmartApp-Architecture/06-Tables-Definitions.md §3.1.
/// </summary>
public sealed record CategoryDto(
    long Id,
    string Name,
    long? ParentId,
    string? Code,
    int SortOrder,
    bool IsActive);
