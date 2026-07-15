using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Catalog.Categories.Dtos;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Categories.Queries.GetCategoryById;

/// <summary>Loads one tenant category by id; NOT_FOUND if it isn't the caller's.</summary>
public sealed class GetCategoryByIdQueryHandler
    : IRequestHandler<GetCategoryByIdQuery, Result<CategoryDto>>
{
    private readonly IApplicationDbContext _db;

    public GetCategoryByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<CategoryDto>> Handle(
        GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        CategoryDto? category = await _db.Categories
            .Where(c => c.Id == request.CategoryId)
            .Select(c => new CategoryDto(c.Id, c.Name, c.ParentId, c.Code, c.SortOrder, c.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        return category is null
            ? Result.Failure<CategoryDto>(Error.NotFound("التصنيف غير موجود."))
            : Result.Success(category);
    }
}
