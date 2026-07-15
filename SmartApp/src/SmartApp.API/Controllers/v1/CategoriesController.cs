using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SmartApp.API.Authorization;
using SmartApp.Application.Catalog.Categories.Commands.CreateCategory;
using SmartApp.Application.Catalog.Categories.Commands.DeleteCategory;
using SmartApp.Application.Catalog.Categories.Commands.UpdateCategory;
using SmartApp.Application.Catalog.Categories.Queries.GetCategories;
using SmartApp.Application.Catalog.Categories.Queries.GetCategoryById;
using SmartApp.Shared.Constants;

namespace SmartApp.API.Controllers.v1;

/// <summary>
/// Catalog categories (tree via ParentId). CRUD guarded by category permissions and scoped to the
/// caller's tenant. See SmartApp-Architecture/06-Tables-Definitions.md §3.1.
/// </summary>
[ApiVersion("1.0")]
public sealed class CategoriesController : ApiControllerBase
{
    /// <summary>Lists the current tenant's categories.</summary>
    [HttpGet]
    [HasPermission(Permissions.Categories.View)]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetCategoriesQuery(), cancellationToken));

    /// <summary>Gets a single category by id.</summary>
    [HttpGet("{id:long}")]
    [HasPermission(Permissions.Categories.View)]
    public async Task<IActionResult> GetCategory(long id, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetCategoryByIdQuery(id), cancellationToken));

    /// <summary>Creates a category. Returns the new id.</summary>
    [HttpPost]
    [HasPermission(Permissions.Categories.Create)]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new CreateCategoryCommand(request.Name, request.ParentId, request.Code, request.SortOrder),
            cancellationToken));

    /// <summary>Updates a category.</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Categories.Update)]
    public async Task<IActionResult> UpdateCategory(
        long id, [FromBody] UpdateCategoryRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new UpdateCategoryCommand(
                id, request.Name, request.ParentId, request.Code, request.SortOrder, request.IsActive),
            cancellationToken));

    /// <summary>Deletes (soft) a category.</summary>
    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Categories.Delete)]
    public async Task<IActionResult> DeleteCategory(long id, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new DeleteCategoryCommand(id), cancellationToken));
}

/// <summary>Create-category request body.</summary>
public sealed record CreateCategoryRequest(string Name, long? ParentId, string? Code, int SortOrder);

/// <summary>Update-category request body.</summary>
public sealed record UpdateCategoryRequest(string Name, long? ParentId, string? Code, int SortOrder, bool IsActive);
