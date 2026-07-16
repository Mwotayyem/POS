using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Inventory.Stock.Commands.AdjustStock;

/// <summary>
/// Adjusts a product's stock in a warehouse by a signed quantity, recording an ADJUST movement.
/// Positive increases stock (uses <see cref="UnitCost"/> for WAC); negative decreases it (at current
/// average cost). See SmartApp-Architecture/13-Development-Rules.md §6.2–6.4.
/// </summary>
public sealed record AdjustStockCommand(
    long ProductId,
    long WarehouseId,
    decimal QuantityChange,
    decimal UnitCost,
    string? Reason) : IRequest<Result>;
