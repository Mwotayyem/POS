namespace SmartApp.Application.Purchasing.PurchaseReturns.Dtos;

/// <summary>A purchase return with its lines.</summary>
public sealed record PurchaseReturnDto(
    long Id,
    string ReturnNumber,
    long PurchaseInvoiceId,
    long WarehouseId,
    DateTime ReturnDate,
    decimal TotalAmount,
    string? Reason,
    IReadOnlyList<PurchaseReturnItemDto> Items);

/// <summary>A purchase return line.</summary>
public sealed record PurchaseReturnItemDto(
    long Id,
    long PurchaseInvoiceItemId,
    long ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);
