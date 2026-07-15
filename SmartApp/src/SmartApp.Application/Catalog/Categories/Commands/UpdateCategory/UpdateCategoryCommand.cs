using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Categories.Commands.UpdateCategory;

/// <summary>
/// Updates a category. Changing <see cref="ParentId"/> is validated to avoid cycles (a category may
/// not be its own ancestor). See SmartApp-Architecture/07-ERD-Relationships.md §5.3.
/// </summary>
public sealed record UpdateCategoryCommand(
    long CategoryId,
    string Name,
    long? ParentId,
    string? Code,
    int SortOrder,
    bool IsActive) : IRequest<Result>;
