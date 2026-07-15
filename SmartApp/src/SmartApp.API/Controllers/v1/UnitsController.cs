using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SmartApp.API.Authorization;
using SmartApp.Application.Catalog.Units.Commands.CreateUnit;
using SmartApp.Application.Catalog.Units.Commands.DeleteUnit;
using SmartApp.Application.Catalog.Units.Commands.UpdateUnit;
using SmartApp.Application.Catalog.Units.Queries.GetUnitById;
using SmartApp.Application.Catalog.Units.Queries.GetUnits;
using SmartApp.Shared.Constants;

namespace SmartApp.API.Controllers.v1;

/// <summary>
/// Units of measure. CRUD guarded by unit permissions and scoped to the caller's tenant.
/// See SmartApp-Architecture/06-Tables-Definitions.md §3.2.
/// </summary>
[ApiVersion("1.0")]
public sealed class UnitsController : ApiControllerBase
{
    /// <summary>Lists the current tenant's units.</summary>
    [HttpGet]
    [HasPermission(Permissions.Units.View)]
    public async Task<IActionResult> GetUnits(CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetUnitsQuery(), cancellationToken));

    /// <summary>Gets a single unit by id.</summary>
    [HttpGet("{id:long}")]
    [HasPermission(Permissions.Units.View)]
    public async Task<IActionResult> GetUnit(long id, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetUnitByIdQuery(id), cancellationToken));

    /// <summary>Creates a unit. Returns the new id.</summary>
    [HttpPost]
    [HasPermission(Permissions.Units.Create)]
    public async Task<IActionResult> CreateUnit(
        [FromBody] CreateUnitRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new CreateUnitCommand(request.Name, request.Symbol, request.Precision), cancellationToken));

    /// <summary>Updates a unit.</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Units.Update)]
    public async Task<IActionResult> UpdateUnit(
        long id, [FromBody] UpdateUnitRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new UpdateUnitCommand(id, request.Name, request.Symbol, request.Precision, request.IsActive),
            cancellationToken));

    /// <summary>Deletes (soft) a unit.</summary>
    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Units.Delete)]
    public async Task<IActionResult> DeleteUnit(long id, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new DeleteUnitCommand(id), cancellationToken));
}

/// <summary>Create-unit request body.</summary>
public sealed record CreateUnitRequest(string Name, string? Symbol, byte Precision);

/// <summary>Update-unit request body.</summary>
public sealed record UpdateUnitRequest(string Name, string? Symbol, byte Precision, bool IsActive);
