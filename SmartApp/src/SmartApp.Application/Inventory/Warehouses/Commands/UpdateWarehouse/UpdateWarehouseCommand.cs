using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Inventory.Warehouses.Commands.UpdateWarehouse;

/// <summary>Updates a warehouse. Name stays unique within the tenant; default is mutually exclusive.</summary>
public sealed record UpdateWarehouseCommand(
    long WarehouseId,
    string Name,
    string? Code,
    string? Address,
    bool IsDefault,
    bool IsActive) : IRequest<Result>;
