namespace SmartApp.Application.Sales.SalesInvoices.Dtos;

/// <summary>A sales invoice header for list views.</summary>
public sealed record SalesInvoiceListItemDto(
    long Id,
    string InvoiceNumber,
    long? CustomerId,
    string? CustomerName,
    long WarehouseId,
    DateTime InvoiceDate,
    byte Status,
    decimal GrandTotal,
    decimal PaidAmount);

/// <summary>A sales invoice with its line items.</summary>
public sealed record SalesInvoiceDto(
    long Id,
    string InvoiceNumber,
    long? CustomerId,
    string? CustomerName,
    long WarehouseId,
    DateTime InvoiceDate,
    byte Status,
    decimal SubTotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    decimal PaidAmount,
    string? Notes,
    IReadOnlyList<SalesInvoiceItemDto> Items);

/// <summary>A sales invoice line.</summary>
public sealed record SalesInvoiceItemDto(
    long Id,
    long ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal UnitCost,
    decimal DiscountAmount,
    decimal TaxRate,
    decimal TaxAmount,
    decimal LineTotal,
    decimal ReturnedQty);
