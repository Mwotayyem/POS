using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SmartApp.API.Authorization;
using SmartApp.Application.Inventory.Stock.Commands.AdjustStock;
using SmartApp.Application.Inventory.Stock.Commands.TransferStock;
using SmartApp.Application.Inventory.Stock.Queries.GetStockBalances;
using SmartApp.Application.Inventory.Stock.Queries.GetStockMovements;
using SmartApp.Shared.Constants;

namespace SmartApp.API.Controllers.v1;

/// <summary>
/// Stock balances, movement history, and the write operations (adjust, transfer). Every stock change
/// generates an append-only movement via the stock ledger (WAC preserved). Guarded by inventory
/// permissions. See SmartApp-Architecture/13-Development-Rules.md §6.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/stock")]
public sealed class StockController : ApiControllerBase
{
    /// <summary>Lists stock balances, optionally filtered by warehouse and/or product.</summary>
    [HttpGet("balances")]
    [HasPermission(Permissions.Stock.View)]
    public async Task<IActionResult> GetBalances(
        [FromQuery] long? warehouseId, [FromQuery] long? productId, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetStockBalancesQuery(warehouseId, productId), cancellationToken));

    /// <summary>Lists the append-only movement history for a product (optionally by warehouse).</summary>
    [HttpGet("movements")]
    [HasPermission(Permissions.Stock.View)]
    public async Task<IActionResult> GetMovements(
        [FromQuery] long productId, [FromQuery] long? warehouseId, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetStockMovementsQuery(productId, warehouseId), cancellationToken));

    /// <summary>Adjusts a product's stock in a warehouse (signed quantity), recording a movement.</summary>
    [HttpPost("adjust")]
    [HasPermission(Permissions.Stock.Adjust)]
    public async Task<IActionResult> Adjust(
        [FromBody] AdjustStockRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new AdjustStockCommand(
                request.ProductId, request.WarehouseId, request.QuantityChange, request.UnitCost, request.Reason),
            cancellationToken));

    /// <summary>Transfers a product's stock between two warehouses (two linked movements).</summary>
    [HttpPost("transfer")]
    [HasPermission(Permissions.Stock.Transfer)]
    public async Task<IActionResult> Transfer(
        [FromBody] TransferStockRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new TransferStockCommand(
                request.ProductId, request.FromWarehouseId, request.ToWarehouseId, request.Quantity, request.Reason),
            cancellationToken));
}

/// <summary>Adjust-stock request body.</summary>
public sealed record AdjustStockRequest(
    long ProductId, long WarehouseId, decimal QuantityChange, decimal UnitCost, string? Reason);

/// <summary>Transfer-stock request body.</summary>
public sealed record TransferStockRequest(
    long ProductId, long FromWarehouseId, long ToWarehouseId, decimal Quantity, string? Reason);
