namespace SmartApp.Application.Inventory.Stock.Dtos;

/// <summary>A stock balance row: a product's quantity and average cost in a warehouse.</summary>
public sealed record StockBalanceDto(
    long Id,
    long ProductId,
    string ProductName,
    long WarehouseId,
    string WarehouseName,
    decimal QtyOnHand,
    decimal AvgCost);

/// <summary>A stock movement row (append-only history).</summary>
public sealed record StockMovementDto(
    long Id,
    long ProductId,
    long WarehouseId,
    byte MovementType,
    decimal QuantityChange,
    decimal UnitCost,
    decimal ResultingQty,
    decimal ResultingAvgCost,
    DateTime OccurredAt,
    string? Reason,
    string? ReferenceCode);
