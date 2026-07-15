using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Catalog;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Brands.Commands.CreateBrand;

/// <summary>Creates a brand; name must be unique within the tenant. TenantId is auto-stamped.</summary>
public sealed class CreateBrandCommandHandler : IRequestHandler<CreateBrandCommand, Result<long>>
{
    private readonly IApplicationDbContext _db;

    public CreateBrandCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(CreateBrandCommand request, CancellationToken cancellationToken)
    {
        string name = request.Name.Trim();
        bool nameTaken = await _db.Brands.AnyAsync(b => b.Name == name, cancellationToken);
        if (nameTaken)
        {
            return Result.Failure<long>(Error.Conflict("اسم العلامة التجارية مستخدم بالفعل."));
        }

        var brand = new Brand
        {
            Name = name,
            Code = request.Code?.Trim(),
            Description = request.Description?.Trim(),
            IsActive = true,
        };
        _db.Brands.Add(brand);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(brand.Id);
    }
}
