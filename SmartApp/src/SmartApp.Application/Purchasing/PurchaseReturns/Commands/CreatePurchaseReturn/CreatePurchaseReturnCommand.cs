using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.PurchaseReturns.Commands.CreatePurchaseReturn;

/// <summary>
/// Creates a purchase return against an invoice: validates return quantities against what remains
/// returnable, posts an outbound stock movement per line (goods leave), reduces the supplier balance,
/// increments each line's ReturnedQty, and updates the invoice status. One transaction. Returns the
/// new return id.
/// </summary>
public sealed record CreatePurchaseReturnCommand(
    long PurchaseInvoiceId,
    DateTime? ReturnDate,
    string? Reason,
    IReadOnlyList<CreatePurchaseReturnLine> Items) : IRequest<Result<long>>;

/// <summary>A line of the return: an original invoice line and the quantity being returned.</summary>
public sealed record CreatePurchaseReturnLine(long PurchaseInvoiceItemId, decimal Quantity);
