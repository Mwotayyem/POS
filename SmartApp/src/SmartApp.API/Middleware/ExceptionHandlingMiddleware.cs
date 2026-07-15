using System.Net;
using System.Text.Json;

namespace SmartApp.API.Middleware;

/// <summary>
/// Global exception handling. Converts any unhandled exception into the unified
/// error envelope described in SmartApp-Architecture/12-API-Architecture.md §4.
/// Phase 1: baseline structure. Application-specific exceptions
/// (ValidationException, NotFoundException, ForbiddenException, BusinessRuleViolationException,
/// concurrency conflicts) are mapped here as they are introduced in later phases.
/// </summary>
public sealed partial class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // Source-generated, allocation-free logging delegate (CA1848 best practice).
    [LoggerMessage(EventId = 1000, Level = LogLevel.Error,
        Message = "Unhandled exception. CorrelationId: {CorrelationId}")]
    private partial void LogUnhandled(Exception ex, string correlationId);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Never leak internal details to the client — log with correlation id, return generic envelope.
            string correlationId = context.TraceIdentifier;
            LogUnhandled(ex, correlationId);
            await WriteErrorAsync(context, HttpStatusCode.InternalServerError, "INTERNAL_ERROR",
                "حدث خطأ غير متوقّع. الرجاء المحاولة لاحقاً.", correlationId);
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string code,
        string message,
        string correlationId)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var payload = new
        {
            success = false,
            data = (object?)null,
            error = new { code, message },
            meta = new { correlationId, timestamp = DateTime.UtcNow.ToString("O") },
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}

/// <summary>Extension to register the global exception handler in the pipeline.</summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionHandlingMiddleware>();
}
