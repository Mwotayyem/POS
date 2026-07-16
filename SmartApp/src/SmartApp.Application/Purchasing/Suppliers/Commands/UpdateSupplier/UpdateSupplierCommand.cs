using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.Suppliers.Commands.UpdateSupplier;

/// <summary>Updates a supplier's details. Balance is system-managed and not settable here.</summary>
public sealed record UpdateSupplierCommand(
    long SupplierId,
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive) : IRequest<Result>;
