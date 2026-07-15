using System.Net.Http.Json;
using System.Text.Json;

namespace SmartApp.IntegrationTests.Api;

/// <summary>
/// Small helpers for reading the unified response envelope (success/data/error/meta) in tests.
/// </summary>
internal static class ApiTestJson
{
    public static async Task<JsonElement> RootAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        // Clone so the value survives disposal of the JsonDocument.
        using JsonDocument doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    public static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        JsonElement root = await RootAsync(response);
        return root.GetProperty("data");
    }

    public static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        JsonElement root = await RootAsync(response);
        return root.GetProperty("error").GetProperty("code").GetString();
    }

    public static async Task<long> ReadIdAsync(HttpResponseMessage response)
    {
        JsonElement data = await DataAsync(response);
        return data.GetInt64();
    }
}
