using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SmartApp.IntegrationTests.Api;

/// <summary>
/// End-to-end tests for the Users management API: list, create, update, activate/deactivate, plus
/// tenant isolation, authorization, and validation.
/// </summary>
public sealed class UsersManagementTests
{
    private const string OwnerEmail = "owner@t1.com";
    private const string Password = "P@ssw0rd!";

    private static async Task<(AdminApiFactory Factory, HttpClient Client)> ArrangeOwnerAsync(long tenantId = 1)
    {
        var factory = new AdminApiFactory();
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedTenantWithOwnerUserAsync(tenantId, OwnerEmail, Password);
        HttpClient client = await factory.CreateAuthenticatedClientAsync(OwnerEmail, Password);
        return (factory, client);
    }

    [Fact]
    public async Task Create_User_Then_List_Returns_It()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage create = await client.PostAsJsonAsync("/api/v1/users", new
        {
            email = "emp@t1.com",
            fullName = "Employee One",
            password = "Str0ng!Pass",
            phone = "0790000000",
            roleIds = Array.Empty<long>(),
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        long newId = await ApiTestJson.ReadIdAsync(create);
        newId.Should().BeGreaterThan(0);

        HttpResponseMessage list = await client.GetAsync("/api/v1/users");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement data = await ApiTestJson.DataAsync(list);
        data.EnumerateArray().Select(u => u.GetProperty("email").GetString())
            .Should().Contain("emp@t1.com");
    }

    [Fact]
    public async Task Create_User_With_Duplicate_Email_Conflicts()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        var body = new { email = "dup@t1.com", fullName = "Dup", password = "Str0ng!Pass", phone = (string?)null, roleIds = Array.Empty<long>() };
        (await client.PostAsJsonAsync("/api/v1/users", body)).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage second = await client.PostAsJsonAsync("/api/v1/users", body);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ApiTestJson.ErrorCodeAsync(second)).Should().Be("BUSINESS_RULE_VIOLATION");
    }

    [Fact]
    public async Task Create_User_With_Invalid_Body_Returns_Validation_Error()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/users", new
        {
            email = "not-an-email",
            fullName = "",
            password = "short",
            phone = (string?)null,
            roleIds = Array.Empty<long>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestJson.ErrorCodeAsync(response)).Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Update_User_Changes_Name_And_Phone()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage create = await client.PostAsJsonAsync("/api/v1/users", new
        {
            email = "edit@t1.com",
            fullName = "Before",
            password = "Str0ng!Pass",
            phone = (string?)null,
            roleIds = Array.Empty<long>(),
        });
        long id = await ApiTestJson.ReadIdAsync(create);

        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/v1/users/{id}", new
        {
            fullName = "After",
            phone = "0788888888",
            roleIds = Array.Empty<long>(),
        });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage get = await client.GetAsync($"/api/v1/users/{id}");
        JsonElement data = await ApiTestJson.DataAsync(get);
        data.GetProperty("fullName").GetString().Should().Be("After");
        data.GetProperty("phone").GetString().Should().Be("0788888888");
    }

    [Fact]
    public async Task Deactivate_Then_Activate_Toggles_IsActive()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage create = await client.PostAsJsonAsync("/api/v1/users", new
        {
            email = "toggle@t1.com",
            fullName = "Toggle",
            password = "Str0ng!Pass",
            phone = (string?)null,
            roleIds = Array.Empty<long>(),
        });
        long id = await ApiTestJson.ReadIdAsync(create);

        (await client.PostAsync($"/api/v1/users/{id}/deactivate", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage afterDeactivate = await client.GetAsync($"/api/v1/users/{id}");
        (await ApiTestJson.DataAsync(afterDeactivate)).GetProperty("isActive").GetBoolean().Should().BeFalse();

        (await client.PostAsync($"/api/v1/users/{id}/activate", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage afterActivate = await client.GetAsync($"/api/v1/users/{id}");
        (await ApiTestJson.DataAsync(afterActivate)).GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Get_User_From_Another_Tenant_Returns_NotFound()
    {
        // Tenant 1 owner + a user in tenant 2 — tenant 1 must not see tenant 2's user.
        var factory = new AdminApiFactory();
        await using var _ = factory;
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedTenantWithOwnerUserAsync(1, OwnerEmail, Password);
        long foreignUserId = await factory.SeedPlainUserAsync(2, "foreign@t2.com", Password);

        HttpClient client = await factory.CreateAuthenticatedClientAsync(OwnerEmail, Password);

        HttpResponseMessage response = await client.GetAsync($"/api/v1/users/{foreignUserId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ApiTestJson.ErrorCodeAsync(response)).Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task User_Without_Permission_Is_Forbidden()
    {
        var factory = new AdminApiFactory();
        await using var _ = factory;
        await factory.SeedPermissionCatalogAsync();
        // A plain user (no roles → no permissions) in an active tenant.
        await factory.SeedPlainUserAsync(1, "plain@t1.com", Password);

        HttpClient client = await factory.CreateAuthenticatedClientAsync("plain@t1.com", Password);

        HttpResponseMessage response = await client.GetAsync("/api/v1/users");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_Request_Is_Unauthorized()
    {
        var factory = new AdminApiFactory();
        await using var _ = factory;
        await factory.SeedPermissionCatalogAsync();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/users");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
