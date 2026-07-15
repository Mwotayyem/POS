using System.Net;
using SmartApp.Shared.Results;

namespace SmartApp.API.Extensions;

/// <summary>
/// Maps a domain <see cref="Error"/> code to its HTTP status, per
/// SmartApp-Architecture/12-API-Architecture.md §3–4. Single source of truth used by both the
/// controllers (Result failures) and the global exception handler.
/// </summary>
public static class ErrorStatusMapper
{
    public static HttpStatusCode ToStatusCode(string errorCode) => errorCode switch
    {
        "VALIDATION_ERROR" => HttpStatusCode.BadRequest,
        "UNAUTHORIZED" => HttpStatusCode.Unauthorized,
        "FORBIDDEN" => HttpStatusCode.Forbidden,
        "TENANT_INACTIVE" => HttpStatusCode.Forbidden,
        "NOT_FOUND" => HttpStatusCode.NotFound,
        "BUSINESS_RULE_VIOLATION" => HttpStatusCode.Conflict,
        "CONCURRENCY_CONFLICT" => HttpStatusCode.Conflict,
        _ => HttpStatusCode.InternalServerError,
    };
}
