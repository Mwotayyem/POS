using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Inventory.Warehouses.Commands.CreateWarehouse;

/// <summary>Creates a warehouse in the current tenant. Returns the new id.</summary>
public sealed record CreateWarehouseCommand(
    string Name,
    string? Code,
    string? Address,
    bool IsDefault) : IRequest<Result<long>>;
