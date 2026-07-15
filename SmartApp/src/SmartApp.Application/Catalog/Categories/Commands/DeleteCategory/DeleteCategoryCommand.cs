using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Categories.Commands.DeleteCategory;

/// <summary>
/// Soft-deletes a category. Refused if the category still has child categories or is linked to
/// products (07-ERD-Relationships.md §4).
/// </summary>
public sealed record DeleteCategoryCommand(long CategoryId) : IRequest<Result>;
