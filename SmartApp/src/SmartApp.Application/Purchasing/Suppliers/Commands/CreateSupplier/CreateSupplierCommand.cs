using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.Suppliers.Commands.CreateSupplier;

/// <summary>Creates a supplier in the current tenant. Balance starts at 0. Returns the new id.</summary>
public sealed record CreateSupplierCommand(
    string Name,
    string? Phone,
    string? Email,
    string? Address) : IRequest<Result<long>>;
