using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Catalog;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Categories.Commands.CreateCategory;

/// <summary>
/// Creates a category, validating that the parent (if given) exists in the caller's tenant. TenantId
/// is auto-stamped (Category is a BaseEntity).
/// </summary>
public sealed class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Result<long>>
{
    private readonly IApplicationDbContext _db;

    public CreateCategoryCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        if (request.ParentId is long parentId)
        {
            bool parentExists = await _db.Categories.AnyAsync(c => c.Id == parentId, cancellationToken);
            if (!parentExists)
            {
                return Result.Failure<long>(Error.Validation(
                    "التصنيف الأب غير موجود.",
                    [new FieldError(nameof(CreateCategoryCommand.ParentId), "معرّف الأب غير صالح.")]));
            }
        }

        var category = new Category
        {
            Name = request.Name.Trim(),
            ParentId = request.ParentId,
            Code = request.Code?.Trim(),
            SortOrder = request.SortOrder,
            IsActive = true,
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(category.Id);
    }
}
