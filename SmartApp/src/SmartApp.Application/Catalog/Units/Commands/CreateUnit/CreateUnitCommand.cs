using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Units.Commands.CreateUnit;

/// <summary>Creates a unit of measure in the current tenant. Returns the new id.</summary>
public sealed record CreateUnitCommand(
    string Name,
    string? Symbol,
    byte Precision) : IRequest<Result<long>>;
