using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Catalog;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Categories.Commands.UpdateCategory;

/// <summary>
/// Updates a category's fields. When the parent changes, walks the ancestor chain to reject cycles
/// (self or descendant as parent). All lookups are tenant-filtered, so cross-tenant parents are
/// invisible and rejected as invalid.
/// </summary>
public sealed class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public UpdateCategoryCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        Category? category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (category is null)
        {
            return Result.Failure(Error.NotFound("التصنيف غير موجود."));
        }

        if (request.ParentId != category.ParentId && request.ParentId is long newParentId)
        {
            Result parentCheck = await ValidateParentAsync(category.Id, newParentId, cancellationToken);
            if (parentCheck.IsFailure)
            {
                return parentCheck;
            }
        }

        category.Name = request.Name.Trim();
        category.ParentId = request.ParentId;
        category.Code = request.Code?.Trim();
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Ensures <paramref name="newParentId"/> exists in the tenant and is not the category itself or
    /// one of its descendants (which would create a cycle). Walks up from the proposed parent to a
    /// root; if it reaches the category being edited, the parent is a descendant.
    /// </summary>
    private async Task<Result> ValidateParentAsync(
        long categoryId, long newParentId, CancellationToken cancellationToken)
    {
        if (newParentId == categoryId)
        {
            return Result.Failure(Error.Validation(
                "لا يمكن أن يكون التصنيف أباً لنفسه.",
                [new FieldError(nameof(UpdateCategoryCommand.ParentId), "أب غير صالح (دورة).")]));
        }

        // Load the tenant's (id -> parentId) map once, then walk ancestors in memory.
        Dictionary<long, long?> parents = await _db.Categories
            .Select(c => new { c.Id, c.ParentId })
            .ToDictionaryAsync(x => x.Id, x => x.ParentId, cancellationToken);

        if (!parents.ContainsKey(newParentId))
        {
            return Result.Failure(Error.Validation(
                "التصنيف الأب غير موجود.",
                [new FieldError(nameof(UpdateCategoryCommand.ParentId), "معرّف الأب غير صالح.")]));
        }

        long? cursor = newParentId;
        while (cursor is long current)
        {
            if (current == categoryId)
            {
                return Result.Failure(Error.Validation(
                    "لا يمكن نقل التصنيف تحت أحد فروعه.",
                    [new FieldError(nameof(UpdateCategoryCommand.ParentId), "أب غير صالح (دورة).")]));
            }

            cursor = parents.TryGetValue(current, out long? parent) ? parent : null;
        }

        return Result.Success();
    }
}
