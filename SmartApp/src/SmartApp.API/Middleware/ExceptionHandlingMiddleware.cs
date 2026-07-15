using System.Net;
using System.Text.Json;
using SmartApp.API.Extensions;
using SmartApp.Application.Common.Exceptions;
using SmartApp.Shared.Results;

namespace SmartApp.API.Middleware;

/// <summary>
/// Global exception handling. Converts unhandled exceptions into the unified error envelope
/// (SmartApp-Architecture/12-API-Architecture.md §4). Known application exceptions are mapped to
/// specific error codes/status; anything else becomes a generic 500 (details logged, never leaked).
/// </summary>
public sealed partial class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    [LoggerMessage(EventId = 1000, Level = LogLevel.Error,
        Message = "Unhandled exception. CorrelationId: {CorrelationId}")]
    private partial void LogUnhandled(Exception ex, string correlationId);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            Error error = Error.Validation("بيانات غير صحيحة.", ex.Errors);
            await WriteAsync(context, HttpStatusCode.BadRequest, error);
        }
        catch (Exception ex)
        {
            string correlationId = context.TraceIdentifier;
            LogUnhandled(ex, correlationId);
            await WriteAsync(context, HttpStatusCode.InternalServerError,
                Error.Internal("حدث خطأ غير متوقّع. الرجاء المحاولة لاحقاً."));
        }
    }

    private static async Task WriteAsync(HttpContext context, HttpStatusCode statusCode, Error error)
    {
        // Don't try to write a body if the response has already started.
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        ApiResponse<object> payload = ApiResponse.Fail<object>(error, context.TraceIdentifier);
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}

/// <summary>Extension to register the global exception handler in the pipeline.</summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionHandlingMiddleware>();
}
