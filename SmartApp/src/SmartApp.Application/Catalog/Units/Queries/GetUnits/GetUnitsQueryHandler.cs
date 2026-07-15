using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Catalog.Units.Dtos;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Units.Queries.GetUnits;

/// <summary>Loads the current tenant's units (tenant-filtered globally).</summary>
public sealed class GetUnitsQueryHandler : IRequestHandler<GetUnitsQuery, Result<IReadOnlyList<UnitDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetUnitsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<UnitDto>>> Handle(
        GetUnitsQuery request, CancellationToken cancellationToken)
    {
        List<UnitDto> units = await _db.Units
            .OrderBy(u => u.Name)
            .Select(u => new UnitDto(u.Id, u.Name, u.Symbol, u.Precision, u.IsActive))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<UnitDto>>(units);
    }
}
