using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.PurchaseInvoices.Commands.CreatePurchaseInvoice;

/// <summary>
/// Creates and confirms a purchase invoice in one transaction: assigns a number, computes totals,
/// posts an inbound stock movement per line (updating WAC via the stock ledger), and increases the
/// supplier balance by the unpaid amount. Optionally marks a source purchase order as received.
/// Returns the new invoice id. See SmartApp-Architecture/13-Development-Rules.md §6.4.
/// </summary>
public sealed record CreatePurchaseInvoiceCommand(
    long SupplierId,
    long WarehouseId,
    DateTime? InvoiceDate,
    decimal PaidAmount,
    string? Notes,
    long? PurchaseOrderId,
    IReadOnlyList<CreatePurchaseInvoiceLine> Items) : IRequest<Result<long>>;

/// <summary>A line on the new purchase invoice.</summary>
public sealed record CreatePurchaseInvoiceLine(
    long ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal TaxRate);
