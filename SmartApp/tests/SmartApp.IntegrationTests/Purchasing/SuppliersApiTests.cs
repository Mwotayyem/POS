using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SmartApp.IntegrationTests.Api;
using Xunit;

namespace SmartApp.IntegrationTests.Purchasing;

/// <summary>
/// End-to-end tests for the Suppliers API: CRUD, uniqueness, tenant isolation, authorization,
/// validation.
/// </summary>
public sealed class SuppliersApiTests
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
    public async Task Create_Update_Delete_Supplier()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        long id = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/suppliers",
            new { name = "Acme Supplies", phone = "0790000000", email = "a@acme.com", address = "Amman" }));

        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/v1/suppliers/{id}",
            new { name = "Acme Supplies Co", phone = (string?)null, email = (string?)null, address = (string?)null, isActive = true });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage get = await client.GetAsync($"/api/v1/suppliers/{id}");
        JsonElement data = await ApiTestJson.DataAsync(get);
        data.GetProperty("name").GetString().Should().Be("Acme Supplies Co");
        data.GetProperty("balance").GetDecimal().Should().Be(0m);

        (await client.DeleteAsync($"/api/v1/suppliers/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/v1/suppliers/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Supplier_With_Duplicate_Name_Conflicts()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        var body = new { name = "Dup", phone = (string?)null, email = (string?)null, address = (string?)null };
        (await client.PostAsJsonAsync("/api/v1/suppliers", body)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/v1/suppliers", body)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_Supplier_With_Invalid_Email_Is_Validation_Error()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage create = await client.PostAsJsonAsync("/api/v1/suppliers",
            new { name = "Bad", phone = (string?)null, email = "not-an-email", address = (string?)null });
        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestJson.ErrorCodeAsync(create)).Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Supplier_From_Another_Tenant_Is_NotFound()
    {
        var factory = new AdminApiFactory();
        await using var _ = factory;
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedTenantWithOwnerUserAsync(1, OwnerEmail, Password);
        await factory.SeedTenantWithOwnerUserAsync(2, "owner@t2.com", Password);

        HttpClient t2 = await factory.CreateAuthenticatedClientAsync("owner@t2.com", Password);
        long t2Supplier = await ApiTestJson.ReadIdAsync(await t2.PostAsJsonAsync("/api/v1/suppliers",
            new { name = "T2 Supplier", phone = (string?)null, email = (string?)null, address = (string?)null }));

        HttpClient t1 = await factory.CreateAuthenticatedClientAsync(OwnerEmail, Password);
        (await t1.GetAsync($"/api/v1/suppliers/{t2Supplier}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Supplier_Without_Permission_Is_Forbidden()
    {
        var factory = new AdminApiFactory();
        await using var _ = factory;
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedPlainUserAsync(1, "plain@t1.com", Password);

        HttpClient client = await factory.CreateAuthenticatedClientAsync("plain@t1.com", Password);
        (await client.GetAsync("/api/v1/suppliers")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
