using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Inventory.Warehouses.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Inventory.Warehouses.Queries.GetWarehouses;

/// <summary>Loads the current tenant's warehouses (tenant-filtered globally).</summary>
public sealed class GetWarehousesQueryHandler
    : IRequestHandler<GetWarehousesQuery, Result<IReadOnlyList<WarehouseDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetWarehousesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<WarehouseDto>>> Handle(
        GetWarehousesQuery request, CancellationToken cancellationToken)
    {
        List<WarehouseDto> warehouses = await _db.Warehouses
            .OrderBy(w => w.Name)
            .Select(w => new WarehouseDto(w.Id, w.Name, w.Code, w.Address, w.IsDefault, w.IsActive))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<WarehouseDto>>(warehouses);
    }
}
