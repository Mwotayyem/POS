using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Categories.Commands.CreateCategory;

/// <summary>
/// Creates a category in the current tenant (optionally under a parent). Returns the new id.
/// TenantId is stamped server-side.
/// </summary>
public sealed record CreateCategoryCommand(
    string Name,
    long? ParentId,
    string? Code,
    int SortOrder) : IRequest<Result<long>>;
