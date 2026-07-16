namespace SmartApp.Application.Reporting.Reports.Dtos;

/// <summary>Sales totals for a single calendar day.</summary>
public sealed record SalesByDayDto(DateOnly Date, decimal Total, int InvoiceCount);

/// <summary>A product whose on-hand quantity is at or below its reorder level.</summary>
public sealed record LowStockItemDto(
    long ProductId,
    string ProductName,
    string? Sku,
    decimal QtyOnHand,
    decimal ReorderLevel);

/// <summary>A product's sales performance over a period.</summary>
public sealed record ProductSalesDto(
    long ProductId,
    string ProductName,
    string? Sku,
    decimal QuantitySold,
    decimal Revenue,
    decimal Cost,
    decimal Profit);
