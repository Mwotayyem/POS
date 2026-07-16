using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.SalesInvoices.Commands.CreateSalesInvoice;

/// <summary>
/// Creates and confirms a sales invoice in one transaction: assigns a number, computes totals,
/// snapshots each line's cost (current WAC) for profitability, posts an outbound stock movement per
/// line (the stock ledger rejects a sale that would drive stock negative), and increases the
/// customer's receivable balance by the unpaid amount. CustomerId may be null (cash sale). Returns
/// the new invoice id. See SmartApp-Architecture/13-Development-Rules.md §6.4.
/// </summary>
public sealed record CreateSalesInvoiceCommand(
    long? CustomerId,
    long WarehouseId,
    DateTime? InvoiceDate,
    decimal PaidAmount,
    string? Notes,
    IReadOnlyList<CreateSalesInvoiceLine> Items) : IRequest<Result<long>>;

/// <summary>A line on the new sales invoice.</summary>
public sealed record CreateSalesInvoiceLine(
    long ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal TaxRate);
