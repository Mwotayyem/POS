using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SmartApp.Domain.Tenancy.Enums;
using SmartApp.IntegrationTests.Api;
using Xunit;

namespace SmartApp.IntegrationTests.Catalog;

/// <summary>
/// End-to-end tests for the Categories API: CRUD, tree parent, cycle prevention, dependency guard,
/// tenant isolation, authorization, and validation.
/// </summary>
public sealed class CategoriesApiTests
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
    public async Task Create_Then_List_And_Get_Category()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage create = await client.PostAsJsonAsync("/api/v1/categories", new
        {
            name = "Beverages",
            parentId = (long?)null,
            code = "BEV",
            sortOrder = 1,
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        long id = await ApiTestJson.ReadIdAsync(create);

        HttpResponseMessage get = await client.GetAsync($"/api/v1/categories/{id}");
        (await ApiTestJson.DataAsync(get)).GetProperty("name").GetString().Should().Be("Beverages");

        HttpResponseMessage list = await client.GetAsync("/api/v1/categories");
        (await ApiTestJson.DataAsync(list)).EnumerateArray()
            .Select(c => c.GetProperty("name").GetString()).Should().Contain("Beverages");
    }

    [Fact]
    public async Task Create_Child_Under_Parent()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        long parentId = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/categories",
            new { name = "Root", parentId = (long?)null, code = (string?)null, sortOrder = 0 }));

        HttpResponseMessage child = await client.PostAsJsonAsync("/api/v1/categories",
            new { name = "Child", parentId, code = (string?)null, sortOrder = 0 });
        child.StatusCode.Should().Be(HttpStatusCode.OK);

        long childId = await ApiTestJson.ReadIdAsync(child);
        HttpResponseMessage get = await client.GetAsync($"/api/v1/categories/{childId}");
        (await ApiTestJson.DataAsync(get)).GetProperty("parentId").GetInt64().Should().Be(parentId);
    }

    [Fact]
    public async Task Update_Category_Cannot_Be_Its_Own_Parent()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        long id = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/categories",
            new { name = "Self", parentId = (long?)null, code = (string?)null, sortOrder = 0 }));

        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/v1/categories/{id}",
            new { name = "Self", parentId = id, code = (string?)null, sortOrder = 0, isActive = true });

        update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestJson.ErrorCodeAsync(update)).Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Update_Category_Cannot_Move_Under_Its_Descendant()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        long rootId = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/categories",
            new { name = "A", parentId = (long?)null, code = (string?)null, sortOrder = 0 }));
        long childId = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/categories",
            new { name = "B", parentId = rootId, code = (string?)null, sortOrder = 0 }));

        // Try to move A under B (its own child) → cycle.
        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/v1/categories/{rootId}",
            new { name = "A", parentId = childId, code = (string?)null, sortOrder = 0, isActive = true });

        update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestJson.ErrorCodeAsync(update)).Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Delete_Category_With_Children_Is_Refused()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        long rootId = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/categories",
            new { name = "Parent", parentId = (long?)null, code = (string?)null, sortOrder = 0 }));
        await client.PostAsJsonAsync("/api/v1/categories",
            new { name = "Kid", parentId = rootId, code = (string?)null, sortOrder = 0 });

        HttpResponseMessage delete = await client.DeleteAsync($"/api/v1/categories/{rootId}");
        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Category_From_Another_Tenant_Is_NotFound()
    {
        var factory = new AdminApiFactory();
        await using var _ = factory;
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedTenantWithOwnerUserAsync(1, OwnerEmail, Password);
        await factory.SeedTenantWithOwnerUserAsync(2, "owner@t2.com", Password);

        HttpClient t2 = await factory.CreateAuthenticatedClientAsync("owner@t2.com", Password);
        long t2CategoryId = await ApiTestJson.ReadIdAsync(await t2.PostAsJsonAsync("/api/v1/categories",
            new { name = "T2 Cat", parentId = (long?)null, code = (string?)null, sortOrder = 0 }));

        // Tenant 1 must not see tenant 2's category.
        HttpClient t1 = await factory.CreateAuthenticatedClientAsync(OwnerEmail, Password);
        HttpResponseMessage get = await t1.GetAsync($"/api/v1/categories/{t2CategoryId}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // And its list must not include it.
        HttpResponseMessage list = await t1.GetAsync("/api/v1/categories");
        (await ApiTestJson.DataAsync(list)).EnumerateArray()
            .Select(c => c.GetProperty("name").GetString()).Should().NotContain("T2 Cat");
    }

    [Fact]
    public async Task Create_Category_Without_Permission_Is_Forbidden()
    {
        var factory = new AdminApiFactory();
        await using var _ = factory;
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedPlainUserAsync(1, "plain@t1.com", Password);

        HttpClient client = await factory.CreateAuthenticatedClientAsync("plain@t1.com", Password);
        HttpResponseMessage create = await client.PostAsJsonAsync("/api/v1/categories",
            new { name = "X", parentId = (long?)null, code = (string?)null, sortOrder = 0 });

        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_Category_With_Empty_Name_Is_Validation_Error()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage create = await client.PostAsJsonAsync("/api/v1/categories",
            new { name = "", parentId = (long?)null, code = (string?)null, sortOrder = 0 });

        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestJson.ErrorCodeAsync(create)).Should().Be("VALIDATION_ERROR");
    }
}
