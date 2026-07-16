using MediatR;
using SmartApp.Application.Inventory.Stock.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Inventory.Stock.Queries.GetStockMovements;

/// <summary>
/// Lists the append-only stock movement history for a product (most recent first), optionally
/// filtered by warehouse.
/// </summary>
public sealed record GetStockMovementsQuery(
    long ProductId,
    long? WarehouseId = null) : IRequest<Result<IReadOnlyList<StockMovementDto>>>;
