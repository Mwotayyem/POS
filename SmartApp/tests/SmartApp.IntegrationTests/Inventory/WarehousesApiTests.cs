using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SmartApp.IntegrationTests.Api;
using Xunit;

namespace SmartApp.IntegrationTests.Inventory;

/// <summary>
/// End-to-end tests for the Warehouses API: CRUD, single-default rule, delete guard, tenant
/// isolation, authorization, and validation.
/// </summary>
public sealed class WarehousesApiTests
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
    public async Task Create_Update_Delete_Warehouse()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        long id = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/warehouses",
            new { name = "Main", code = "M1", address = "Downtown", isDefault = true }));

        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/v1/warehouses/{id}",
            new { name = "Main WH", code = "M1", address = (string?)null, isDefault = true, isActive = true });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage get = await client.GetAsync($"/api/v1/warehouses/{id}");
        (await ApiTestJson.DataAsync(get)).GetProperty("name").GetString().Should().Be("Main WH");

        (await client.DeleteAsync($"/api/v1/warehouses/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/v1/warehouses/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Only_One_Default_Warehouse_Remains()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        long first = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/warehouses",
            new { name = "W1", code = (string?)null, address = (string?)null, isDefault = true }));
        long second = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/warehouses",
            new { name = "W2", code = (string?)null, address = (string?)null, isDefault = true }));

        JsonElement list = await ApiTestJson.DataAsync(await client.GetAsync("/api/v1/warehouses"));
        int defaults = list.EnumerateArray().Count(w => w.GetProperty("isDefault").GetBoolean());
        defaults.Should().Be(1);

        // The second one is the surviving default.
        list.EnumerateArray().First(w => w.GetProperty("id").GetInt64() == second)
            .GetProperty("isDefault").GetBoolean().Should().BeTrue();
        list.EnumerateArray().First(w => w.GetProperty("id").GetInt64() == first)
            .GetProperty("isDefault").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Create_Warehouse_With_Duplicate_Name_Conflicts()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        var body = new { name = "Dup", code = (string?)null, address = (string?)null, isDefault = false };
        (await client.PostAsJsonAsync("/api/v1/warehouses", body)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/v1/warehouses", body)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Warehouse_From_Another_Tenant_Is_NotFound()
    {
        var factory = new AdminApiFactory();
        await using var _ = factory;
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedTenantWithOwnerUserAsync(1, OwnerEmail, Password);
        await factory.SeedTenantWithOwnerUserAsync(2, "owner@t2.com", Password);

        HttpClient t2 = await factory.CreateAuthenticatedClientAsync("owner@t2.com", Password);
        long t2Warehouse = await ApiTestJson.ReadIdAsync(await t2.PostAsJsonAsync("/api/v1/warehouses",
            new { name = "T2 WH", code = (string?)null, address = (string?)null, isDefault = false }));

        HttpClient t1 = await factory.CreateAuthenticatedClientAsync(OwnerEmail, Password);
        (await t1.GetAsync($"/api/v1/warehouses/{t2Warehouse}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Warehouse_Without_Permission_Is_Forbidden()
    {
        var factory = new AdminApiFactory();
        await using var _ = factory;
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedPlainUserAsync(1, "plain@t1.com", Password);

        HttpClient client = await factory.CreateAuthenticatedClientAsync("plain@t1.com", Password);
        (await client.GetAsync("/api/v1/warehouses")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_Warehouse_With_Empty_Name_Is_Validation_Error()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage create = await client.PostAsJsonAsync("/api/v1/warehouses",
            new { name = "", code = (string?)null, address = (string?)null, isDefault = false });
        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestJson.ErrorCodeAsync(create)).Should().Be("VALIDATION_ERROR");
    }
}
