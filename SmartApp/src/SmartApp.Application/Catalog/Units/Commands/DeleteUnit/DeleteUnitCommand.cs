using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Units.Commands.DeleteUnit;

/// <summary>
/// Soft-deletes a unit. Refused if it is a product's base unit or referenced by any product-unit.
/// </summary>
public sealed record DeleteUnitCommand(long UnitId) : IRequest<Result>;
