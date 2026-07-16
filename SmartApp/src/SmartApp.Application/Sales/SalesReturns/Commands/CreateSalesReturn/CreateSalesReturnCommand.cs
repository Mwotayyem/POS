using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.SalesReturns.Commands.CreateSalesReturn;

/// <summary>
/// Creates a sales return against an invoice: validates return quantities against what remains
/// returnable, posts an inbound stock movement per line at the original cost (goods come back),
/// reduces the customer balance, increments each line's ReturnedQty, and updates the invoice status.
/// One transaction. Returns the new return id.
/// </summary>
public sealed record CreateSalesReturnCommand(
    long SalesInvoiceId,
    DateTime? ReturnDate,
    string? Reason,
    IReadOnlyList<CreateSalesReturnLine> Items) : IRequest<Result<long>>;

/// <summary>A line of the return: an original invoice line and the quantity being returned.</summary>
public sealed record CreateSalesReturnLine(long SalesInvoiceItemId, decimal Quantity);
