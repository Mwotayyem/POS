using MediatR;
using SmartApp.Application.Catalog.Categories.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Categories.Queries.GetCategoryById;

/// <summary>Fetches one category of the current tenant by id. NOT_FOUND if absent.</summary>
public sealed record GetCategoryByIdQuery(long CategoryId) : IRequest<Result<CategoryDto>>;
