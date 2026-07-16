using MediatR;
using SmartApp.Application.Purchasing.Suppliers.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.Suppliers.Queries.GetSupplierById;

/// <summary>Fetches one supplier of the current tenant by id. NOT_FOUND if absent.</summary>
public sealed record GetSupplierByIdQuery(long SupplierId) : IRequest<Result<SupplierDto>>;
