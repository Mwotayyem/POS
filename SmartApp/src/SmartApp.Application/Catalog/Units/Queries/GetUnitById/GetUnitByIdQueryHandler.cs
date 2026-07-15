using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Catalog.Units.Dtos;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Units.Queries.GetUnitById;

/// <summary>Loads one tenant unit by id; NOT_FOUND if it isn't the caller's.</summary>
public sealed class GetUnitByIdQueryHandler : IRequestHandler<GetUnitByIdQuery, Result<UnitDto>>
{
    private readonly IApplicationDbContext _db;

    public GetUnitByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<UnitDto>> Handle(GetUnitByIdQuery request, CancellationToken cancellationToken)
    {
        UnitDto? unit = await _db.Units
            .Where(u => u.Id == request.UnitId)
            .Select(u => new UnitDto(u.Id, u.Name, u.Symbol, u.Precision, u.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        return unit is null
            ? Result.Failure<UnitDto>(Error.NotFound("وحدة القياس غير موجودة."))
            : Result.Success(unit);
    }
}
