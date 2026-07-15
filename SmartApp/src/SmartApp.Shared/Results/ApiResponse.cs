namespace SmartApp.Shared.Results;

/// <summary>
/// The uniform response envelope returned by every API endpoint (success or failure), matching
/// SmartApp-Architecture/12-API-Architecture.md §2.
/// </summary>
/// <typeparam name="T">The payload type on success.</typeparam>
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public Error? Error { get; init; }
    public ApiMeta Meta { get; init; } = new();
}

/// <summary>Factory helpers for <see cref="ApiResponse{T}"/> (non-generic to satisfy CA1000).</summary>
public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data, string correlationId) => new()
    {
        Success = true,
        Data = data,
        Meta = ApiMeta.Create(correlationId),
    };

    public static ApiResponse<T> Fail<T>(Error error, string correlationId) => new()
    {
        Success = false,
        Error = error,
        Meta = ApiMeta.Create(correlationId),
    };
}

/// <summary>Envelope metadata: correlation id + UTC timestamp.</summary>
public sealed class ApiMeta
{
    public string CorrelationId { get; init; } = string.Empty;
    public string Timestamp { get; init; } = string.Empty;

    public static ApiMeta Create(string correlationId) => new()
    {
        CorrelationId = correlationId,
        Timestamp = DateTime.UtcNow.ToString("O"),
    };
}
