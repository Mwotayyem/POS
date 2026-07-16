using MediatR;
using SmartApp.Application.Purchasing.PurchaseInvoices.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.PurchaseInvoices.Queries.GetPurchaseInvoices;

/// <summary>Lists the current tenant's purchase invoices, optionally filtered by supplier.</summary>
public sealed record GetPurchaseInvoicesQuery(long? SupplierId = null)
    : IRequest<Result<IReadOnlyList<PurchaseInvoiceListItemDto>>>;
