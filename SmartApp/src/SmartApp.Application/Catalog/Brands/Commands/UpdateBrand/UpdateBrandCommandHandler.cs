using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Catalog;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Brands.Commands.UpdateBrand;

/// <summary>Updates a brand, enforcing name uniqueness within the tenant (excluding itself).</summary>
public sealed class UpdateBrandCommandHandler : IRequestHandler<UpdateBrandCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public UpdateBrandCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(UpdateBrandCommand request, CancellationToken cancellationToken)
    {
        Brand? brand = await _db.Brands.FirstOrDefaultAsync(b => b.Id == request.BrandId, cancellationToken);
        if (brand is null)
        {
            return Result.Failure(Error.NotFound("العلامة التجارية غير موجودة."));
        }

        string name = request.Name.Trim();
        bool nameTaken = await _db.Brands
            .AnyAsync(b => b.Id != brand.Id && b.Name == name, cancellationToken);
        if (nameTaken)
        {
            return Result.Failure(Error.Conflict("اسم العلامة التجارية مستخدم بالفعل."));
        }

        brand.Name = name;
        brand.Code = request.Code?.Trim();
        brand.Description = request.Description?.Trim();
        brand.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
