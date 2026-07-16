using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Inventory;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Inventory.Warehouses.Commands.CreateWarehouse;

/// <summary>
/// Creates a warehouse; name must be unique within the tenant. If flagged default, any existing
/// default is cleared so at most one default warehouse exists per tenant. TenantId is auto-stamped.
/// </summary>
public sealed class CreateWarehouseCommandHandler : IRequestHandler<CreateWarehouseCommand, Result<long>>
{
    private readonly IApplicationDbContext _db;

    public CreateWarehouseCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(CreateWarehouseCommand request, CancellationToken cancellationToken)
    {
        string name = request.Name.Trim();
        if (await _db.Warehouses.AnyAsync(w => w.Name == name, cancellationToken))
        {
            return Result.Failure<long>(Error.Conflict("اسم المستودع مستخدم بالفعل."));
        }

        if (request.IsDefault)
        {
            await ClearExistingDefaultAsync(cancellationToken);
        }

        var warehouse = new Warehouse
        {
            Name = name,
            Code = request.Code?.Trim(),
            Address = request.Address?.Trim(),
            IsDefault = request.IsDefault,
            IsActive = true,
        };
        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(warehouse.Id);
    }

    private async Task ClearExistingDefaultAsync(CancellationToken cancellationToken)
    {
        List<Warehouse> defaults = await _db.Warehouses
            .Where(w => w.IsDefault)
            .ToListAsync(cancellationToken);
        foreach (Warehouse w in defaults)
        {
            w.IsDefault = false;
        }
    }
}
