using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Inventory.Warehouses.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Inventory.Warehouses.Queries.GetWarehouseById;

/// <summary>Loads one tenant warehouse by id; NOT_FOUND if it isn't the caller's.</summary>
public sealed class GetWarehouseByIdQueryHandler
    : IRequestHandler<GetWarehouseByIdQuery, Result<WarehouseDto>>
{
    private readonly IApplicationDbContext _db;

    public GetWarehouseByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<WarehouseDto>> Handle(
        GetWarehouseByIdQuery request, CancellationToken cancellationToken)
    {
        WarehouseDto? warehouse = await _db.Warehouses
            .Where(w => w.Id == request.WarehouseId)
            .Select(w => new WarehouseDto(w.Id, w.Name, w.Code, w.Address, w.IsDefault, w.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        return warehouse is null
            ? Result.Failure<WarehouseDto>(Error.NotFound("المستودع غير موجود."))
            : Result.Success(warehouse);
    }
}
