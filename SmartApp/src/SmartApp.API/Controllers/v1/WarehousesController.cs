using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SmartApp.API.Authorization;
using SmartApp.Application.Inventory.Warehouses.Commands.CreateWarehouse;
using SmartApp.Application.Inventory.Warehouses.Commands.DeleteWarehouse;
using SmartApp.Application.Inventory.Warehouses.Commands.UpdateWarehouse;
using SmartApp.Application.Inventory.Warehouses.Queries.GetWarehouseById;
using SmartApp.Application.Inventory.Warehouses.Queries.GetWarehouses;
using SmartApp.Shared.Constants;

namespace SmartApp.API.Controllers.v1;

/// <summary>
/// Warehouses (stock locations). CRUD guarded by warehouse permissions and scoped to the caller's
/// tenant.
/// </summary>
[ApiVersion("1.0")]
public sealed class WarehousesController : ApiControllerBase
{
    /// <summary>Lists the current tenant's warehouses.</summary>
    [HttpGet]
    [HasPermission(Permissions.Warehouses.View)]
    public async Task<IActionResult> GetWarehouses(CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetWarehousesQuery(), cancellationToken));

    /// <summary>Gets a single warehouse by id.</summary>
    [HttpGet("{id:long}")]
    [HasPermission(Permissions.Warehouses.View)]
    public async Task<IActionResult> GetWarehouse(long id, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetWarehouseByIdQuery(id), cancellationToken));

    /// <summary>Creates a warehouse. Returns the new id.</summary>
    [HttpPost]
    [HasPermission(Permissions.Warehouses.Create)]
    public async Task<IActionResult> CreateWarehouse(
        [FromBody] CreateWarehouseRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new CreateWarehouseCommand(request.Name, request.Code, request.Address, request.IsDefault),
            cancellationToken));

    /// <summary>Updates a warehouse.</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Warehouses.Update)]
    public async Task<IActionResult> UpdateWarehouse(
        long id, [FromBody] UpdateWarehouseRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new UpdateWarehouseCommand(
                id, request.Name, request.Code, request.Address, request.IsDefault, request.IsActive),
            cancellationToken));

    /// <summary>Deletes (soft) a warehouse.</summary>
    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Warehouses.Delete)]
    public async Task<IActionResult> DeleteWarehouse(long id, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new DeleteWarehouseCommand(id), cancellationToken));
}

/// <summary>Create-warehouse request body.</summary>
public sealed record CreateWarehouseRequest(string Name, string? Code, string? Address, bool IsDefault);

/// <summary>Update-warehouse request body.</summary>
public sealed record UpdateWarehouseRequest(string Name, string? Code, string? Address, bool IsDefault, bool IsActive);
