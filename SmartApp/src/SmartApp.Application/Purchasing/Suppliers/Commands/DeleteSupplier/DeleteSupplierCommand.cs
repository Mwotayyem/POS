using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.Suppliers.Commands.DeleteSupplier;

/// <summary>Soft-deletes a supplier. Refused if any purchase order or invoice references it.</summary>
public sealed record DeleteSupplierCommand(long SupplierId) : IRequest<Result>;
