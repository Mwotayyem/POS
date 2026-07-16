using MediatR;
using SmartApp.Application.Sales.SalesInvoices.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.SalesInvoices.Queries.GetSalesInvoiceById;

/// <summary>Fetches one sales invoice of the current tenant with its items. NOT_FOUND if absent.</summary>
public sealed record GetSalesInvoiceByIdQuery(long InvoiceId) : IRequest<Result<SalesInvoiceDto>>;
