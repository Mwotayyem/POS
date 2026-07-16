using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Inventory;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Inventory.Warehouses.Commands.UpdateWarehouse;

/// <summary>
/// Updates a warehouse, enforcing name uniqueness (excluding itself). Promoting it to default clears
/// any other default so at most one default exists per tenant.
/// </summary>
public sealed class UpdateWarehouseCommandHandler : IRequestHandler<UpdateWarehouseCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public UpdateWarehouseCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(UpdateWarehouseCommand request, CancellationToken cancellationToken)
    {
        Warehouse? warehouse = await _db.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken);
        if (warehouse is null)
        {
            return Result.Failure(Error.NotFound("المستودع غير موجود."));
        }

        string name = request.Name.Trim();
        bool nameTaken = await _db.Warehouses
            .AnyAsync(w => w.Id != warehouse.Id && w.Name == name, cancellationToken);
        if (nameTaken)
        {
            return Result.Failure(Error.Conflict("اسم المستودع مستخدم بالفعل."));
        }

        if (request.IsDefault && !warehouse.IsDefault)
        {
            List<Warehouse> defaults = await _db.Warehouses
                .Where(w => w.IsDefault && w.Id != warehouse.Id)
                .ToListAsync(cancellationToken);
            foreach (Warehouse w in defaults)
            {
                w.IsDefault = false;
            }
        }

        warehouse.Name = name;
        warehouse.Code = request.Code?.Trim();
        warehouse.Address = request.Address?.Trim();
        warehouse.IsDefault = request.IsDefault;
        warehouse.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
