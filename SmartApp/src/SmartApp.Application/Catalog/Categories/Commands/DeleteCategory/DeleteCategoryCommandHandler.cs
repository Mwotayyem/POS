using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Catalog;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Categories.Commands.DeleteCategory;

/// <summary>
/// Soft-deletes a category after guarding: a category with children or linked products is refused
/// (the dependency check lives in the Application layer since FKs are NO ACTION).
/// </summary>
public sealed class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public DeleteCategoryCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        Category? category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (category is null)
        {
            return Result.Failure(Error.NotFound("التصنيف غير موجود."));
        }

        bool hasChildren = await _db.Categories.AnyAsync(c => c.ParentId == category.Id, cancellationToken);
        if (hasChildren)
        {
            return Result.Failure(Error.Conflict("لا يمكن حذف تصنيف يحتوي على تصنيفات فرعية."));
        }

        bool hasProducts = await _db.Products.AnyAsync(p => p.CategoryId == category.Id, cancellationToken);
        if (hasProducts)
        {
            return Result.Failure(Error.Conflict("لا يمكن حذف تصنيف مرتبط بمنتجات."));
        }

        _db.Categories.Remove(category); // Interceptor converts to soft delete.
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
