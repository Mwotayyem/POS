namespace SmartApp.Application.Sales.SalesReturns.Dtos;

/// <summary>A sales return with its lines.</summary>
public sealed record SalesReturnDto(
    long Id,
    string ReturnNumber,
    long SalesInvoiceId,
    long WarehouseId,
    DateTime ReturnDate,
    decimal TotalAmount,
    string? Reason,
    IReadOnlyList<SalesReturnItemDto> Items);

/// <summary>A sales return line.</summary>
public sealed record SalesReturnItemDto(
    long Id,
    long SalesInvoiceItemId,
    long ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);
