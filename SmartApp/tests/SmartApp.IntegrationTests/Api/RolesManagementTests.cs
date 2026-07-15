using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SmartApp.Shared.Constants;
using Xunit;

namespace SmartApp.IntegrationTests.Api;

/// <summary>
/// End-to-end tests for the Roles management API: CRUD, permission assignment, system-role
/// protection, and validation of unknown permission codes.
/// </summary>
public sealed class RolesManagementTests
{
    private const string OwnerEmail = "owner@t1.com";
    private const string Password = "P@ssw0rd!";

    private static async Task<(AdminApiFactory Factory, HttpClient Client)> ArrangeOwnerAsync()
    {
        var factory = new AdminApiFactory();
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedTenantWithOwnerUserAsync(1, OwnerEmail, Password);
        HttpClient client = await factory.CreateAuthenticatedClientAsync(OwnerEmail, Password);
        return (factory, client);
    }

    [Fact]
    public async Task Create_Role_With_Permissions_Then_Get_Returns_Them()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage create = await client.PostAsJsonAsync("/api/v1/roles", new
        {
            name = "Cashier",
            description = "Front desk",
            permissions = new[] { Permissions.Users.View, Permissions.Roles.View },
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        long roleId = await ApiTestJson.ReadIdAsync(create);

        HttpResponseMessage get = await client.GetAsync($"/api/v1/roles/{roleId}");
        JsonElement data = await ApiTestJson.DataAsync(get);
        data.GetProperty("name").GetString().Should().Be("Cashier");
        data.GetProperty("permissions").EnumerateArray().Select(p => p.GetString())
            .Should().BeEquivalentTo(new[] { Permissions.Users.View, Permissions.Roles.View });
    }

    [Fact]
    public async Task Create_Role_With_Unknown_Permission_Returns_Validation_Error()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage create = await client.PostAsJsonAsync("/api/v1/roles", new
        {
            name = "Bad",
            description = (string?)null,
            permissions = new[] { "nonexistent.permission" },
        });

        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestJson.ErrorCodeAsync(create)).Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Set_Role_Permissions_Replaces_The_Set()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage create = await client.PostAsJsonAsync("/api/v1/roles", new
        {
            name = "Manager",
            description = (string?)null,
            permissions = new[] { Permissions.Users.View },
        });
        long roleId = await ApiTestJson.ReadIdAsync(create);

        HttpResponseMessage set = await client.PutAsJsonAsync($"/api/v1/roles/{roleId}/permissions", new
        {
            permissions = new[] { Permissions.Roles.View, Permissions.Roles.Create },
        });
        set.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage get = await client.GetAsync($"/api/v1/roles/{roleId}");
        JsonElement data = await ApiTestJson.DataAsync(get);
        data.GetProperty("permissions").EnumerateArray().Select(p => p.GetString())
            .Should().BeEquivalentTo(new[] { Permissions.Roles.View, Permissions.Roles.Create });
    }

    [Fact]
    public async Task Update_Then_Delete_Custom_Role()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage create = await client.PostAsJsonAsync("/api/v1/roles", new
        {
            name = "Temp",
            description = (string?)null,
            permissions = Array.Empty<string>(),
        });
        long roleId = await ApiTestJson.ReadIdAsync(create);

        (await client.PutAsJsonAsync($"/api/v1/roles/{roleId}", new { name = "Temp2", description = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.DeleteAsync($"/api/v1/roles/{roleId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync($"/api/v1/roles/{roleId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_System_Owner_Role_Is_Refused()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        // Find the seeded system Owner role.
        HttpResponseMessage list = await client.GetAsync("/api/v1/roles");
        JsonElement data = await ApiTestJson.DataAsync(list);
        long ownerRoleId = data.EnumerateArray()
            .First(r => r.GetProperty("name").GetString() == RoleNames.Owner)
            .GetProperty("id").GetInt64();

        HttpResponseMessage delete = await client.DeleteAsync($"/api/v1/roles/{ownerRoleId}");
        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ApiTestJson.ErrorCodeAsync(delete)).Should().Be("BUSINESS_RULE_VIOLATION");
    }

    [Fact]
    public async Task Owner_Role_Seeded_With_All_Permissions()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage list = await client.GetAsync("/api/v1/roles");
        JsonElement data = await ApiTestJson.DataAsync(list);
        JsonElement owner = data.EnumerateArray()
            .First(r => r.GetProperty("name").GetString() == RoleNames.Owner);

        int grantedCount = owner.GetProperty("permissions").GetArrayLength();
        grantedCount.Should().Be(PermissionCatalog.All.Count);
    }
}
