namespace SmartApp.Application.Purchasing.PurchaseOrders.Dtos;

/// <summary>A purchase order header for list views.</summary>
public sealed record PurchaseOrderListItemDto(
    long Id,
    string OrderNumber,
    long SupplierId,
    string SupplierName,
    DateTime OrderDate,
    byte Status,
    decimal Total);

/// <summary>A purchase order with its lines.</summary>
public sealed record PurchaseOrderDto(
    long Id,
    string OrderNumber,
    long SupplierId,
    string SupplierName,
    DateTime OrderDate,
    byte Status,
    decimal Total,
    string? Notes,
    IReadOnlyList<PurchaseOrderItemDto> Items);

/// <summary>A purchase order line.</summary>
public sealed record PurchaseOrderItemDto(
    long Id,
    long ProductId,
    decimal Quantity,
    decimal UnitCost,
    decimal LineTotal);
