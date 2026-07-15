using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SmartApp.Domain.Tenancy.Enums;
using Xunit;

namespace SmartApp.IntegrationTests.Api;

/// <summary>
/// End-to-end tests for the authentication endpoints, exercising the full HTTP pipeline
/// (middleware → MediatR → DbContext). Covers the scenarios in Phase 5:
/// successful login, wrong password, inactive-tenant block, refresh rotation, and logout revoke.
/// </summary>
public sealed class AuthEndpointsTests
{
    private const string Email = "owner@t1.com";
    private const string Password = "P@ssw0rd!";

    [Fact]
    public async Task Login_Succeeds_With_Valid_Credentials()
    {
        await using var factory = new AuthApiFactory();
        await factory.SeedTenantAndUserAsync(1, TenantStatus.Active, Email, Password);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = Email, password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (string? access, string? refresh) = await ReadTokensAsync(response);
        access.Should().NotBeNullOrWhiteSpace();
        refresh.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_Fails_With_Wrong_Password()
    {
        await using var factory = new AuthApiFactory();
        await factory.SeedTenantAndUserAsync(1, TenantStatus.Active, Email, Password);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = Email, password = "wrong-password" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ReadErrorCodeAsync(response)).Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Login_Is_Blocked_For_Inactive_Tenant()
    {
        await using var factory = new AuthApiFactory();
        await factory.SeedTenantAndUserAsync(1, TenantStatus.Suspended, Email, Password);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = Email, password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadErrorCodeAsync(response)).Should().Be("TENANT_INACTIVE");
    }

    [Fact]
    public async Task Refresh_Rotates_The_Token_And_Revokes_The_Old_One()
    {
        await using var factory = new AuthApiFactory();
        await factory.SeedTenantAndUserAsync(1, TenantStatus.Active, Email, Password);
        HttpClient client = factory.CreateClient();

        // Login → first token pair.
        HttpResponseMessage login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = Email, password = Password });
        (_, string? refresh1) = await ReadTokensAsync(login);

        // Refresh → new pair, different refresh token.
        HttpResponseMessage refresh = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = refresh1 });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        (_, string? refresh2) = await ReadTokensAsync(refresh);
        refresh2.Should().NotBeNullOrWhiteSpace();
        refresh2.Should().NotBe(refresh1, "rotation must issue a new refresh token");

        // The old token is now revoked — reusing it fails.
        HttpResponseMessage reused = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = refresh1 });
        reused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Revokes_The_Refresh_Token()
    {
        await using var factory = new AuthApiFactory();
        await factory.SeedTenantAndUserAsync(1, TenantStatus.Active, Email, Password);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = Email, password = Password });
        (_, string? refresh) = await ReadTokensAsync(login);

        // Logout revokes it (204).
        HttpResponseMessage logout = await client.PostAsJsonAsync(
            "/api/v1/auth/logout", new { refreshToken = refresh });
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The revoked token can no longer be refreshed.
        HttpResponseMessage afterLogout = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = refresh });
        afterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_Returns_Envelope_Validation_Error_For_Empty_Body()
    {
        await using var factory = new AuthApiFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = "", password = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        // Unified envelope shape (not the framework default): success/data/error/meta.
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        doc.RootElement.GetProperty("error").GetProperty("details").GetArrayLength().Should().BeGreaterThan(0);
    }

    // ---- helpers ----

    private static async Task<(string? Access, string? Refresh)> ReadTokensAsync(HttpResponseMessage response)
    {
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement data = doc.RootElement.GetProperty("data");
        return (
            data.GetProperty("accessToken").GetString(),
            data.GetProperty("refreshToken").GetString());
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("error").GetProperty("code").GetString();
    }
}
