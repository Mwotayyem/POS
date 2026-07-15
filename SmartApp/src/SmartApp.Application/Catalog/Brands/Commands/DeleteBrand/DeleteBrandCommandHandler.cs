using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Catalog;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Brands.Commands.DeleteBrand;

/// <summary>Soft-deletes a brand after guarding that no product references it.</summary>
public sealed class DeleteBrandCommandHandler : IRequestHandler<DeleteBrandCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public DeleteBrandCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(DeleteBrandCommand request, CancellationToken cancellationToken)
    {
        Brand? brand = await _db.Brands.FirstOrDefaultAsync(b => b.Id == request.BrandId, cancellationToken);
        if (brand is null)
        {
            return Result.Failure(Error.NotFound("العلامة التجارية غير موجودة."));
        }

        bool inUse = await _db.Products.AnyAsync(p => p.BrandId == brand.Id, cancellationToken);
        if (inUse)
        {
            return Result.Failure(Error.Conflict("لا يمكن حذف علامة تجارية مرتبطة بمنتجات."));
        }

        _db.Brands.Remove(brand);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
