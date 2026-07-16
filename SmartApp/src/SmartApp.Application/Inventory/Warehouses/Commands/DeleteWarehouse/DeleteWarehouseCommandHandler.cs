using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Inventory;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Inventory.Warehouses.Commands.DeleteWarehouse;

/// <summary>
/// Soft-deletes a warehouse after guarding against in-use references (any stock balance or recorded
/// movement). Movements are append-only history and must never be orphaned.
/// </summary>
public sealed class DeleteWarehouseCommandHandler : IRequestHandler<DeleteWarehouseCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public DeleteWarehouseCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(DeleteWarehouseCommand request, CancellationToken cancellationToken)
    {
        Warehouse? warehouse = await _db.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken);
        if (warehouse is null)
        {
            return Result.Failure(Error.NotFound("المستودع غير موجود."));
        }

        bool hasStock = await _db.Stocks.AnyAsync(s => s.WarehouseId == warehouse.Id, cancellationToken);
        bool hasMovements = await _db.StockMovements.AnyAsync(m => m.WarehouseId == warehouse.Id, cancellationToken);
        if (hasStock || hasMovements)
        {
            return Result.Failure(Error.Conflict("لا يمكن حذف مستودع يحتوي على مخزون أو حركات."));
        }

        _db.Warehouses.Remove(warehouse);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
