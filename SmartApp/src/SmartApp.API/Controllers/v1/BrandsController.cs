using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SmartApp.API.Authorization;
using SmartApp.Application.Catalog.Brands.Commands.CreateBrand;
using SmartApp.Application.Catalog.Brands.Commands.DeleteBrand;
using SmartApp.Application.Catalog.Brands.Commands.UpdateBrand;
using SmartApp.Application.Catalog.Brands.Queries.GetBrandById;
using SmartApp.Application.Catalog.Brands.Queries.GetBrands;
using SmartApp.Shared.Constants;

namespace SmartApp.API.Controllers.v1;

/// <summary>Brands. CRUD guarded by brand permissions and scoped to the caller's tenant.</summary>
[ApiVersion("1.0")]
public sealed class BrandsController : ApiControllerBase
{
    /// <summary>Lists the current tenant's brands.</summary>
    [HttpGet]
    [HasPermission(Permissions.Brands.View)]
    public async Task<IActionResult> GetBrands(CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetBrandsQuery(), cancellationToken));

    /// <summary>Gets a single brand by id.</summary>
    [HttpGet("{id:long}")]
    [HasPermission(Permissions.Brands.View)]
    public async Task<IActionResult> GetBrand(long id, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetBrandByIdQuery(id), cancellationToken));

    /// <summary>Creates a brand. Returns the new id.</summary>
    [HttpPost]
    [HasPermission(Permissions.Brands.Create)]
    public async Task<IActionResult> CreateBrand(
        [FromBody] CreateBrandRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new CreateBrandCommand(request.Name, request.Code, request.Description), cancellationToken));

    /// <summary>Updates a brand.</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Brands.Update)]
    public async Task<IActionResult> UpdateBrand(
        long id, [FromBody] UpdateBrandRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new UpdateBrandCommand(id, request.Name, request.Code, request.Description, request.IsActive),
            cancellationToken));

    /// <summary>Deletes (soft) a brand.</summary>
    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Brands.Delete)]
    public async Task<IActionResult> DeleteBrand(long id, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new DeleteBrandCommand(id), cancellationToken));
}

/// <summary>Create-brand request body.</summary>
public sealed record CreateBrandRequest(string Name, string? Code, string? Description);

/// <summary>Update-brand request body.</summary>
public sealed record UpdateBrandRequest(string Name, string? Code, string? Description, bool IsActive);
