using MediatR;
using SmartApp.Application.Purchasing.Suppliers.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.Suppliers.Queries.GetSuppliers;

/// <summary>Lists the current tenant's suppliers, ordered by name, with optional name/phone search.</summary>
public sealed record GetSuppliersQuery(string? Search = null) : IRequest<Result<IReadOnlyList<SupplierDto>>>;
