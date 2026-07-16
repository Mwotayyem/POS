using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Inventory.Warehouses.Commands.DeleteWarehouse;

/// <summary>Soft-deletes a warehouse. Refused if it holds any stock or has recorded movements.</summary>
public sealed record DeleteWarehouseCommand(long WarehouseId) : IRequest<Result>;
