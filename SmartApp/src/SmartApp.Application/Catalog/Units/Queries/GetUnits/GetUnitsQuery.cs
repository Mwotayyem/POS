using MediatR;
using SmartApp.Application.Catalog.Units.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Units.Queries.GetUnits;

/// <summary>Lists the current tenant's units of measure, ordered by name.</summary>
public sealed record GetUnitsQuery : IRequest<Result<IReadOnlyList<UnitDto>>>;
