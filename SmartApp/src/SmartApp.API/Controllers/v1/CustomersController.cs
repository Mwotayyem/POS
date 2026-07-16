using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SmartApp.API.Authorization;
using SmartApp.Application.Sales.Customers.Commands.CreateCustomer;
using SmartApp.Application.Sales.Customers.Commands.DeleteCustomer;
using SmartApp.Application.Sales.Customers.Commands.UpdateCustomer;
using SmartApp.Application.Sales.Customers.Queries.GetCustomerById;
using SmartApp.Application.Sales.Customers.Queries.GetCustomers;
using SmartApp.Shared.Constants;

namespace SmartApp.API.Controllers.v1;

/// <summary>Customers. CRUD guarded by customer permissions and scoped to the caller's tenant.</summary>
[ApiVersion("1.0")]
public sealed class CustomersController : ApiControllerBase
{
    /// <summary>Lists the current tenant's customers (optional name/phone search).</summary>
    [HttpGet]
    [HasPermission(Permissions.Customers.View)]
    public async Task<IActionResult> GetCustomers([FromQuery] string? search, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetCustomersQuery(search), cancellationToken));

    /// <summary>Gets a single customer by id.</summary>
    [HttpGet("{id:long}")]
    [HasPermission(Permissions.Customers.View)]
    public async Task<IActionResult> GetCustomer(long id, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new GetCustomerByIdQuery(id), cancellationToken));

    /// <summary>Creates a customer. Returns the new id.</summary>
    [HttpPost]
    [HasPermission(Permissions.Customers.Create)]
    public async Task<IActionResult> CreateCustomer(
        [FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new CreateCustomerCommand(request.Name, request.Phone, request.Email, request.Address, request.CreditLimit),
            cancellationToken));

    /// <summary>Updates a customer.</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Customers.Update)]
    public async Task<IActionResult> UpdateCustomer(
        long id, [FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(
            new UpdateCustomerCommand(
                id, request.Name, request.Phone, request.Email, request.Address, request.CreditLimit, request.IsActive),
            cancellationToken));

    /// <summary>Deletes (soft) a customer.</summary>
    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Customers.Delete)]
    public async Task<IActionResult> DeleteCustomer(long id, CancellationToken cancellationToken)
        => ToResponse(await Mediator.Send(new DeleteCustomerCommand(id), cancellationToken));
}

/// <summary>Create-customer request body.</summary>
public sealed record CreateCustomerRequest(string Name, string? Phone, string? Email, string? Address, decimal CreditLimit);

/// <summary>Update-customer request body.</summary>
public sealed record UpdateCustomerRequest(
    string Name, string? Phone, string? Email, string? Address, decimal CreditLimit, bool IsActive);
