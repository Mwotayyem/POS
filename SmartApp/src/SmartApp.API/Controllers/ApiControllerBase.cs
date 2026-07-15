using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartApp.API.Extensions;
using SmartApp.Shared.Results;

namespace SmartApp.API.Controllers;

/// <summary>
/// Base controller: exposes MediatR and converts a <see cref="Result"/> into the uniform response
/// envelope with the correct HTTP status. Keeps controllers thin
/// (SmartApp-Architecture/13-Development-Rules.md §1).
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;

    protected ISender Mediator =>
        _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    /// <summary>Maps a value-bearing result to 200 (success) or the mapped error status.</summary>
    protected IActionResult ToResponse<T>(Result<T> result)
    {
        string correlationId = HttpContext.TraceIdentifier;

        if (result.IsSuccess)
        {
            return Ok(ApiResponse.Ok(result.Value!, correlationId));
        }

        Error error = result.Error!;
        int status = (int)ErrorStatusMapper.ToStatusCode(error.Code);
        return StatusCode(status, ApiResponse.Fail<T>(error, correlationId));
    }

    /// <summary>Maps a valueless result to 204 (success) or the mapped error status.</summary>
    protected IActionResult ToResponse(Result result)
    {
        string correlationId = HttpContext.TraceIdentifier;

        if (result.IsSuccess)
        {
            return NoContent();
        }

        Error error = result.Error!;
        int status = (int)ErrorStatusMapper.ToStatusCode(error.Code);
        return StatusCode(status, ApiResponse.Fail<object>(error, correlationId));
    }
}
