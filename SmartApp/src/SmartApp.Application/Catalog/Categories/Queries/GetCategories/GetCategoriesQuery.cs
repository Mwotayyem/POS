using MediatR;
using SmartApp.Application.Catalog.Categories.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Categories.Queries.GetCategories;

/// <summary>Lists the current tenant's categories, ordered by SortOrder then Name.</summary>
public sealed record GetCategoriesQuery : IRequest<Result<IReadOnlyList<CategoryDto>>>;
