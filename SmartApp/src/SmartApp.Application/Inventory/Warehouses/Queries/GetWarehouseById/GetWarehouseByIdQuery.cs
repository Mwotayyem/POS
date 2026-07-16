using MediatR;
using SmartApp.Application.Inventory.Warehouses.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Inventory.Warehouses.Queries.GetWarehouseById;

/// <summary>Fetches one warehouse of the current tenant by id. NOT_FOUND if absent.</summary>
public sealed record GetWarehouseByIdQuery(long WarehouseId) : IRequest<Result<WarehouseDto>>;
