using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Catalog.Brands.Dtos;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Brands.Queries.GetBrandById;

/// <summary>Loads one tenant brand by id; NOT_FOUND if it isn't the caller's.</summary>
public sealed class GetBrandByIdQueryHandler : IRequestHandler<GetBrandByIdQuery, Result<BrandDto>>
{
    private readonly IApplicationDbContext _db;

    public GetBrandByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<BrandDto>> Handle(GetBrandByIdQuery request, CancellationToken cancellationToken)
    {
        BrandDto? brand = await _db.Brands
            .Where(b => b.Id == request.BrandId)
            .Select(b => new BrandDto(b.Id, b.Name, b.Code, b.Description, b.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        return brand is null
            ? Result.Failure<BrandDto>(Error.NotFound("العلامة التجارية غير موجودة."))
            : Result.Success(brand);
    }
}
