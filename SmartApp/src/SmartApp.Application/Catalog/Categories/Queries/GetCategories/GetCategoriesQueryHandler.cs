using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Catalog.Categories.Dtos;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Categories.Queries.GetCategories;

/// <summary>Loads the current tenant's categories (tenant-filtered globally).</summary>
public sealed class GetCategoriesQueryHandler
    : IRequestHandler<GetCategoriesQuery, Result<IReadOnlyList<CategoryDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetCategoriesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(
        GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        List<CategoryDto> categories = await _db.Categories
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.ParentId, c.Code, c.SortOrder, c.IsActive))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CategoryDto>>(categories);
    }
}
