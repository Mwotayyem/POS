using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SmartApp.API.Authorization;
using SmartApp.Application.Purchasing.Suppliers.Commands.CreateSupplier;
using SmartApp.Application.Purchasing.Suppliers.Commands.DeleteSupplier;
using SmartApp.Application.Purchasing.Suppliers.Commands.UpdateSupplier;
using SmartApp.Application.Purchasing.Suppliers.Queries.GetSupplierById;
using SmartApp.Application.Purchasing.Suppliers.Queries.GetSuppliers;
using SmartApp.Shared.Constants;

namespace SmartApp.API.Controllers.v1;

/// <summary>Suppliers. CRUD guarded by supplier permissions and scoped to the caller's tenant.</summary>
[ApiVersion("1.0")]
public sealed class SuppliersController : ApiControllerBase
{
    /// <summary>Lists the current tenant's suppliers (optional name/phone search).</summary>
    [HttpGet]
    [HasPermission(Permissions.Suppliers.View)]
    public async Task<IActionResult> GetSuppliers([FromQuery] string? search, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetSuppliersQuery(search), cancellationToken));

    /// <summary>Gets a single supplier by id.</summary>
    [HttpGet("{id:long}")]
    [HasPermission(Permissions.Suppliers.View)]
    public async Task<IActionResult> GetSupplier(long id, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetSupplierByIdQuery(id), cancellationToken));

    /// <summary>Creates a supplier. Returns the new id.</summary>
    [HttpPost]
    [HasPermission(Permissions.Suppliers.Create)]
    public async Task<IActionResult> CreateSupplier(
        [FromBody] CreateSupplierRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new CreateSupplierCommand(request.Name, request.Phone, request.Email, request.Address),
            cancellationToken));

    /// <summary>Updates a supplier.</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Suppliers.Update)]
    public async Task<IActionResult> UpdateSupplier(
        long id, [FromBody] UpdateSupplierRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new UpdateSupplierCommand(id, request.Name, request.Phone, request.Email, request.Address, request.IsActive),
            cancellationToken));

    /// <summary>Deletes (soft) a supplier.</summary>
    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Suppliers.Delete)]
    public async Task<IActionResult> DeleteSupplier(long id, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new DeleteSupplierCommand(id), cancellationToken));
}

/// <summary>Create-supplier request body.</summary>
public sealed record CreateSupplierRequest(string Name, string? Phone, string? Email, string? Address);

/// <summary>Update-supplier request body.</summary>
public sealed record UpdateSupplierRequest(string Name, string? Phone, string? Email, string? Address, bool IsActive);
