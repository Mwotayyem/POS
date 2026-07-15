using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Shared.Results;
using UnitEntity = SmartApp.Domain.Catalog.Unit;

namespace SmartApp.Application.Catalog.Units.Commands.CreateUnit;

/// <summary>Creates a unit; name must be unique within the tenant. TenantId is auto-stamped.</summary>
public sealed class CreateUnitCommandHandler : IRequestHandler<CreateUnitCommand, Result<long>>
{
    private readonly IApplicationDbContext _db;

    public CreateUnitCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(CreateUnitCommand request, CancellationToken cancellationToken)
    {
        string name = request.Name.Trim();
        bool nameTaken = await _db.Units.AnyAsync(u => u.Name == name, cancellationToken);
        if (nameTaken)
        {
            return Result.Failure<long>(Error.Conflict("اسم وحدة القياس مستخدم بالفعل."));
        }

        var unit = new UnitEntity
        {
            Name = name,
            Symbol = request.Symbol?.Trim(),
            Precision = request.Precision,
            IsActive = true,
        };
        _db.Units.Add(unit);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(unit.Id);
    }
}
