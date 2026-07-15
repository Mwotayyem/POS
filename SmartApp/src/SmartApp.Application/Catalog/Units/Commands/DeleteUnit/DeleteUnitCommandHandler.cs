using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Shared.Results;
using UnitEntity = SmartApp.Domain.Catalog.Unit;

namespace SmartApp.Application.Catalog.Units.Commands.DeleteUnit;

/// <summary>
/// Soft-deletes a unit after guarding against in-use references (base unit of a product, or a
/// product-unit conversion row). Dependency checks live in the Application layer (NO ACTION FKs).
/// </summary>
public sealed class DeleteUnitCommandHandler : IRequestHandler<DeleteUnitCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public DeleteUnitCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(DeleteUnitCommand request, CancellationToken cancellationToken)
    {
        UnitEntity? unit = await _db.Units.FirstOrDefaultAsync(u => u.Id == request.UnitId, cancellationToken);
        if (unit is null)
        {
            return Result.Failure(Error.NotFound("وحدة القياس غير موجودة."));
        }

        bool isBaseUnit = await _db.Products.AnyAsync(p => p.BaseUnitId == unit.Id, cancellationToken);
        bool inProductUnits = await _db.ProductUnits.AnyAsync(pu => pu.UnitId == unit.Id, cancellationToken);
        if (isBaseUnit || inProductUnits)
        {
            return Result.Failure(Error.Conflict("لا يمكن حذف وحدة قياس مستخدمة في منتجات."));
        }

        _db.Units.Remove(unit);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
