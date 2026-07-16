using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SmartApp.IntegrationTests.Api;
using Xunit;

namespace SmartApp.IntegrationTests.Sales;

/// <summary>
/// End-to-end tests for the Customers API: CRUD, uniqueness, tenant isolation, authorization,
/// validation.
/// </summary>
public sealed class CustomersApiTests
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
    public async Task Create_Update_Delete_Customer()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        long id = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/customers",
            new { name = "Beta Buyer", phone = "0791111111", email = "b@beta.com", address = "Irbid", creditLimit = 1000m }));

        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/v1/customers/{id}",
            new { name = "Beta Buyer Ltd", phone = (string?)null, email = (string?)null, address = (string?)null, creditLimit = 2000m, isActive = true });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage get = await client.GetAsync($"/api/v1/customers/{id}");
        JsonElement data = await ApiTestJson.DataAsync(get);
        data.GetProperty("name").GetString().Should().Be("Beta Buyer Ltd");
        data.GetProperty("creditLimit").GetDecimal().Should().Be(2000m);
        data.GetProperty("balance").GetDecimal().Should().Be(0m);

        (await client.DeleteAsync($"/api/v1/customers/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/v1/customers/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Customer_With_Duplicate_Name_Conflicts()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        var body = new { name = "Dup", phone = (string?)null, email = (string?)null, address = (string?)null, creditLimit = 0m };
        (await client.PostAsJsonAsync("/api/v1/customers", body)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/v1/customers", body)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Customer_From_Another_Tenant_Is_NotFound()
    {
        var factory = new AdminApiFactory();
        await using var _ = factory;
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedTenantWithOwnerUserAsync(1, OwnerEmail, Password);
        await factory.SeedTenantWithOwnerUserAsync(2, "owner@t2.com", Password);

        HttpClient t2 = await factory.CreateAuthenticatedClientAsync("owner@t2.com", Password);
        long t2Customer = await ApiTestJson.ReadIdAsync(await t2.PostAsJsonAsync("/api/v1/customers",
            new { name = "T2 Customer", phone = (string?)null, email = (string?)null, address = (string?)null, creditLimit = 0m }));

        HttpClient t1 = await factory.CreateAuthenticatedClientAsync(OwnerEmail, Password);
        (await t1.GetAsync($"/api/v1/customers/{t2Customer}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Customer_Without_Permission_Is_Forbidden()
    {
        var factory = new AdminApiFactory();
        await using var _ = factory;
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedPlainUserAsync(1, "plain@t1.com", Password);

        HttpClient client = await factory.CreateAuthenticatedClientAsync("plain@t1.com", Password);
        (await client.GetAsync("/api/v1/customers")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
