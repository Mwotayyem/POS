namespace SmartApp.Application.Purchasing.PurchaseInvoices.Dtos;

/// <summary>A purchase invoice header for list views.</summary>
public sealed record PurchaseInvoiceListItemDto(
    long Id,
    string InvoiceNumber,
    long SupplierId,
    string SupplierName,
    long WarehouseId,
    DateTime InvoiceDate,
    byte Status,
    decimal GrandTotal,
    decimal PaidAmount);

/// <summary>A purchase invoice with its line items.</summary>
public sealed record PurchaseInvoiceDto(
    long Id,
    string InvoiceNumber,
    long SupplierId,
    string SupplierName,
    long WarehouseId,
    DateTime InvoiceDate,
    byte Status,
    decimal SubTotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    decimal PaidAmount,
    string? Notes,
    IReadOnlyList<PurchaseInvoiceItemDto> Items);

/// <summary>A purchase invoice line.</summary>
public sealed record PurchaseInvoiceItemDto(
    long Id,
    long ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal TaxRate,
    decimal TaxAmount,
    decimal LineTotal,
    decimal ReturnedQty);
