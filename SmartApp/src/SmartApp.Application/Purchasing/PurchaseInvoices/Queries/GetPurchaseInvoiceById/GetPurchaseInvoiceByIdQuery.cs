using MediatR;
using SmartApp.Application.Purchasing.PurchaseInvoices.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.PurchaseInvoices.Queries.GetPurchaseInvoiceById;

/// <summary>Fetches one purchase invoice of the current tenant with its items. NOT_FOUND if absent.</summary>
public sealed record GetPurchaseInvoiceByIdQuery(long InvoiceId) : IRequest<Result<PurchaseInvoiceDto>>;
