using MediatR;
using SmartApp.Application.Catalog.Units.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Units.Queries.GetUnitById;

/// <summary>Fetches one unit of the current tenant by id. NOT_FOUND if absent.</summary>
public sealed record GetUnitByIdQuery(long UnitId) : IRequest<Result<UnitDto>>;
