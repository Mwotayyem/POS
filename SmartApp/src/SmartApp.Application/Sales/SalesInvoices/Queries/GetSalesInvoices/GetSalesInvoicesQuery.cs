using MediatR;
using SmartApp.Application.Sales.SalesInvoices.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.SalesInvoices.Queries.GetSalesInvoices;

/// <summary>Lists the current tenant's sales invoices, optionally filtered by customer.</summary>
public sealed record GetSalesInvoicesQuery(long? CustomerId = null)
    : IRequest<Result<IReadOnlyList<SalesInvoiceListItemDto>>>;
