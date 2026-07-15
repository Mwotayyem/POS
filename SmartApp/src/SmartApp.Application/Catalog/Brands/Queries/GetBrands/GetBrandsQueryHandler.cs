using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Catalog.Brands.Dtos;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Brands.Queries.GetBrands;

/// <summary>Loads the current tenant's brands (tenant-filtered globally).</summary>
public sealed class GetBrandsQueryHandler : IRequestHandler<GetBrandsQuery, Result<IReadOnlyList<BrandDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetBrandsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<BrandDto>>> Handle(
        GetBrandsQuery request, CancellationToken cancellationToken)
    {
        List<BrandDto> brands = await _db.Brands
            .OrderBy(b => b.Name)
            .Select(b => new BrandDto(b.Id, b.Name, b.Code, b.Description, b.IsActive))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<BrandDto>>(brands);
    }
}
