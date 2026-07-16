using MediatR;
using SmartApp.Application.Inventory.Warehouses.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Inventory.Warehouses.Queries.GetWarehouses;

/// <summary>Lists the current tenant's warehouses, ordered by name.</summary>
public sealed record GetWarehousesQuery : IRequest<Result<IReadOnlyList<WarehouseDto>>>;
