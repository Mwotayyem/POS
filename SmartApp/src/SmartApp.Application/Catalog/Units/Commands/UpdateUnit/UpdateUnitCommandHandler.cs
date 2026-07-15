using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Shared.Results;
using UnitEntity = SmartApp.Domain.Catalog.Unit;

namespace SmartApp.Application.Catalog.Units.Commands.UpdateUnit;

/// <summary>Updates a unit, enforcing name uniqueness within the tenant (excluding itself).</summary>
public sealed class UpdateUnitCommandHandler : IRequestHandler<UpdateUnitCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public UpdateUnitCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(UpdateUnitCommand request, CancellationToken cancellationToken)
    {
        UnitEntity? unit = await _db.Units.FirstOrDefaultAsync(u => u.Id == request.UnitId, cancellationToken);
        if (unit is null)
        {
            return Result.Failure(Error.NotFound("وحدة القياس غير موجودة."));
        }

        string name = request.Name.Trim();
        bool nameTaken = await _db.Units
            .AnyAsync(u => u.Id != unit.Id && u.Name == name, cancellationToken);
        if (nameTaken)
        {
            return Result.Failure(Error.Conflict("اسم وحدة القياس مستخدم بالفعل."));
        }

        unit.Name = name;
        unit.Symbol = request.Symbol?.Trim();
        unit.Precision = request.Precision;
        unit.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
