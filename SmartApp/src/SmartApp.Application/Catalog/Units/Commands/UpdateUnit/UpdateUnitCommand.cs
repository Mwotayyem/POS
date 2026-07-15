using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Units.Commands.UpdateUnit;

/// <summary>Updates a unit of measure. Name stays unique within the tenant.</summary>
public sealed record UpdateUnitCommand(
    long UnitId,
    string Name,
    string? Symbol,
    byte Precision,
    bool IsActive) : IRequest<Result>;
