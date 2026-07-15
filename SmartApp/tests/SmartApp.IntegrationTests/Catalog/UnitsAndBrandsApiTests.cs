using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SmartApp.IntegrationTests.Api;
using Xunit;

namespace SmartApp.IntegrationTests.Catalog;

/// <summary>
/// End-to-end tests for the Units and Brands APIs: CRUD, uniqueness, tenant isolation, authorization,
/// and validation.
/// </summary>
public sealed class UnitsAndBrandsApiTests
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

    // ---- Units ----

    [Fact]
    public async Task Create_Update_Delete_Unit()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        long id = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/units",
            new { name = "Piece", symbol = "pc", precision = (byte)0 }));

        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/v1/units/{id}",
            new { name = "Piece", symbol = "PCS", precision = (byte)0, isActive = true });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage get = await client.GetAsync($"/api/v1/units/{id}");
        (await ApiTestJson.DataAsync(get)).GetProperty("symbol").GetString().Should().Be("PCS");

        (await client.DeleteAsync($"/api/v1/units/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/v1/units/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Unit_With_Duplicate_Name_Conflicts()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        var body = new { name = "Kg", symbol = "kg", precision = (byte)3 };
        (await client.PostAsJsonAsync("/api/v1/units", body)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/v1/units", body)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_Unit_With_Precision_Above_6_Is_Validation_Error()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage create = await client.PostAsJsonAsync("/api/v1/units",
            new { name = "Weird", symbol = (string?)null, precision = (byte)9 });

        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestJson.ErrorCodeAsync(create)).Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Units_Are_Isolated_By_Tenant()
    {
        var factory = new AdminApiFactory();
        await using var _ = factory;
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedTenantWithOwnerUserAsync(1, OwnerEmail, Password);
        await factory.SeedTenantWithOwnerUserAsync(2, "owner@t2.com", Password);

        HttpClient t2 = await factory.CreateAuthenticatedClientAsync("owner@t2.com", Password);
        long t2Unit = await ApiTestJson.ReadIdAsync(await t2.PostAsJsonAsync("/api/v1/units",
            new { name = "T2 Unit", symbol = (string?)null, precision = (byte)0 }));

        HttpClient t1 = await factory.CreateAuthenticatedClientAsync(OwnerEmail, Password);
        (await t1.GetAsync($"/api/v1/units/{t2Unit}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- Brands ----

    [Fact]
    public async Task Create_Update_Delete_Brand()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        long id = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/brands",
            new { name = "Acme", code = "ACM", description = "Test brand" }));

        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/v1/brands/{id}",
            new { name = "Acme Corp", code = "ACM", description = (string?)null, isActive = true });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage get = await client.GetAsync($"/api/v1/brands/{id}");
        (await ApiTestJson.DataAsync(get)).GetProperty("name").GetString().Should().Be("Acme Corp");

        (await client.DeleteAsync($"/api/v1/brands/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/v1/brands/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Brand_With_Duplicate_Name_Conflicts()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        var body = new { name = "Dup", code = (string?)null, description = (string?)null };
        (await client.PostAsJsonAsync("/api/v1/brands", body)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/v1/brands", body)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Brand_Without_Permission_Is_Forbidden()
    {
        var factory = new AdminApiFactory();
        await using var _ = factory;
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedPlainUserAsync(1, "plain@t1.com", Password);

        HttpClient client = await factory.CreateAuthenticatedClientAsync("plain@t1.com", Password);
        (await client.GetAsync("/api/v1/brands")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
