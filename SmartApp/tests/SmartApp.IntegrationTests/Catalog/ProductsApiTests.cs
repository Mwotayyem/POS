using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SmartApp.IntegrationTests.Api;
using Xunit;

namespace SmartApp.IntegrationTests.Catalog;

/// <summary>
/// End-to-end tests for the Products API, including child collections (units, barcodes, prices):
/// CRUD, SKU/barcode uniqueness, single-primary-barcode rule, tenant isolation, authorization,
/// and validation of references.
/// </summary>
public sealed class ProductsApiTests
{
    private const string OwnerEmail = "owner@t1.com";
    private const string Password = "P@ssw0rd!";

    private sealed record CatalogContext(AdminApiFactory Factory, HttpClient Client, long UnitId, long CategoryId, long BrandId);

    /// <summary>Owner client + a seeded unit, category, and brand (so products can reference them).</summary>
    private static async Task<CatalogContext> ArrangeCatalogAsync(string email = OwnerEmail, long tenantId = 1)
    {
        var factory = new AdminApiFactory();
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedTenantWithOwnerUserAsync(tenantId, email, Password);
        HttpClient client = await factory.CreateAuthenticatedClientAsync(email, Password);

        long unitId = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/units",
            new { name = "Piece", symbol = "pc", precision = (byte)0 }));
        long categoryId = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/categories",
            new { name = "General", parentId = (long?)null, code = (string?)null, sortOrder = 0 }));
        long brandId = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/brands",
            new { name = "Acme", code = (string?)null, description = (string?)null }));

        return new CatalogContext(factory, client, unitId, categoryId, brandId);
    }

    private static object BuildProduct(
        CatalogContext ctx, string name, string? sku,
        object[]? barcodes = null, object[]? prices = null, object[]? units = null)
        => new
        {
            name,
            sku,
            categoryId = ctx.CategoryId,
            brandId = ctx.BrandId,
            baseUnitId = ctx.UnitId,
            costPrice = 5m,
            salePrice = 10m,
            taxRate = 15m,
            reorderLevel = 3m,
            trackStock = true,
            customFieldsJson = (string?)null,
            units = units ?? Array.Empty<object>(),
            barcodes = barcodes ?? Array.Empty<object>(),
            prices = prices ?? Array.Empty<object>(),
        };

    [Fact]
    public async Task Create_Product_With_Children_Then_Get_Returns_Them()
    {
        CatalogContext ctx = await ArrangeCatalogAsync();
        await using var _ = ctx.Factory;

        object body = BuildProduct(ctx, "Cola", "SKU-1",
            barcodes: [new { barcode = "1111", isPrimary = true }, new { barcode = "2222", isPrimary = false }],
            prices: [new { priceType = (byte)1, amount = 10m }, new { priceType = (byte)2, amount = 8m }],
            units: [new { unitId = ctx.UnitId, conversionFactor = 1m, barcode = (string?)null }]);

        HttpResponseMessage create = await ctx.Client.PostAsJsonAsync("/api/v1/products", body);
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        long id = await ApiTestJson.ReadIdAsync(create);

        HttpResponseMessage get = await ctx.Client.GetAsync($"/api/v1/products/{id}");
        JsonElement data = await ApiTestJson.DataAsync(get);
        data.GetProperty("name").GetString().Should().Be("Cola");
        data.GetProperty("sku").GetString().Should().Be("SKU-1");
        data.GetProperty("barcodes").GetArrayLength().Should().Be(2);
        data.GetProperty("prices").GetArrayLength().Should().Be(2);
        data.GetProperty("units").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Create_Product_With_Duplicate_Sku_Conflicts()
    {
        CatalogContext ctx = await ArrangeCatalogAsync();
        await using var _ = ctx.Factory;

        (await ctx.Client.PostAsJsonAsync("/api/v1/products", BuildProduct(ctx, "A", "DUP")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await ctx.Client.PostAsJsonAsync("/api/v1/products", BuildProduct(ctx, "B", "DUP")))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_Product_With_Two_Primary_Barcodes_Is_Validation_Error()
    {
        CatalogContext ctx = await ArrangeCatalogAsync();
        await using var _ = ctx.Factory;

        object body = BuildProduct(ctx, "BadBarcodes", null,
            barcodes: [new { barcode = "AAA", isPrimary = true }, new { barcode = "BBB", isPrimary = true }]);

        HttpResponseMessage create = await ctx.Client.PostAsJsonAsync("/api/v1/products", body);
        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestJson.ErrorCodeAsync(create)).Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Create_Product_With_Duplicate_Barcode_Across_Products_Conflicts()
    {
        CatalogContext ctx = await ArrangeCatalogAsync();
        await using var _ = ctx.Factory;

        (await ctx.Client.PostAsJsonAsync("/api/v1/products",
            BuildProduct(ctx, "First", "S1", barcodes: [new { barcode = "SHARED", isPrimary = true }])))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage second = await ctx.Client.PostAsJsonAsync("/api/v1/products",
            BuildProduct(ctx, "Second", "S2", barcodes: [new { barcode = "SHARED", isPrimary = true }]));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_Product_With_Invalid_BaseUnit_Is_Validation_Error()
    {
        CatalogContext ctx = await ArrangeCatalogAsync();
        await using var _ = ctx.Factory;

        object body = new
        {
            name = "NoUnit",
            sku = (string?)null,
            categoryId = (long?)null,
            brandId = (long?)null,
            baseUnitId = 999999L, // does not exist
            costPrice = 1m,
            salePrice = 1m,
            taxRate = 0m,
            reorderLevel = 0m,
            trackStock = true,
            customFieldsJson = (string?)null,
            units = Array.Empty<object>(),
            barcodes = Array.Empty<object>(),
            prices = Array.Empty<object>(),
        };

        HttpResponseMessage create = await ctx.Client.PostAsJsonAsync("/api/v1/products", body);
        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestJson.ErrorCodeAsync(create)).Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Update_Product_Replaces_Children()
    {
        CatalogContext ctx = await ArrangeCatalogAsync();
        await using var _ = ctx.Factory;

        long id = await ApiTestJson.ReadIdAsync(await ctx.Client.PostAsJsonAsync("/api/v1/products",
            BuildProduct(ctx, "P", "P-1",
                barcodes: [new { barcode = "OLD", isPrimary = true }],
                prices: [new { priceType = (byte)1, amount = 10m }])));

        object update = new
        {
            name = "P Updated",
            sku = "P-1",
            categoryId = ctx.CategoryId,
            brandId = ctx.BrandId,
            baseUnitId = ctx.UnitId,
            costPrice = 6m,
            salePrice = 12m,
            taxRate = 15m,
            reorderLevel = 2m,
            isActive = true,
            trackStock = true,
            customFieldsJson = (string?)null,
            units = Array.Empty<object>(),
            barcodes = new[] { new { barcode = "NEW", isPrimary = true } },
            prices = new[] { new { priceType = (byte)2, amount = 9m } },
        };

        (await ctx.Client.PutAsJsonAsync($"/api/v1/products/{id}", update))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement data = await ApiTestJson.DataAsync(await ctx.Client.GetAsync($"/api/v1/products/{id}"));
        data.GetProperty("name").GetString().Should().Be("P Updated");
        data.GetProperty("barcodes").GetArrayLength().Should().Be(1);
        data.GetProperty("barcodes")[0].GetProperty("barcode").GetString().Should().Be("NEW");
        data.GetProperty("prices")[0].GetProperty("priceType").GetByte().Should().Be(2);
    }

    [Fact]
    public async Task Delete_Product_Removes_It()
    {
        CatalogContext ctx = await ArrangeCatalogAsync();
        await using var _ = ctx.Factory;

        long id = await ApiTestJson.ReadIdAsync(await ctx.Client.PostAsJsonAsync("/api/v1/products",
            BuildProduct(ctx, "Doomed", "DEL-1", barcodes: [new { barcode = "DELBC", isPrimary = true }])));

        (await ctx.Client.DeleteAsync($"/api/v1/products/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ctx.Client.GetAsync($"/api/v1/products/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_Products_Filters_By_Search()
    {
        CatalogContext ctx = await ArrangeCatalogAsync();
        await using var _ = ctx.Factory;

        await ctx.Client.PostAsJsonAsync("/api/v1/products", BuildProduct(ctx, "Apple Juice", "AJ"));
        await ctx.Client.PostAsJsonAsync("/api/v1/products", BuildProduct(ctx, "Orange Soda", "OS"));

        JsonElement data = await ApiTestJson.DataAsync(await ctx.Client.GetAsync("/api/v1/products?search=Apple"));
        data.EnumerateArray().Select(p => p.GetProperty("name").GetString())
            .Should().Contain("Apple Juice").And.NotContain("Orange Soda");
    }

    [Fact]
    public async Task Product_From_Another_Tenant_Is_NotFound()
    {
        CatalogContext ctxT2 = await ArrangeCatalogAsync("owner@t2.com", tenantId: 2);
        await using var _ = ctxT2.Factory;

        long t2Product = await ApiTestJson.ReadIdAsync(await ctxT2.Client.PostAsJsonAsync("/api/v1/products",
            BuildProduct(ctxT2, "T2 Product", "T2-1")));

        // Seed tenant 1 owner in the SAME factory and authenticate.
        await ctxT2.Factory.SeedTenantWithOwnerUserAsync(1, OwnerEmail, Password);
        HttpClient t1 = await ctxT2.Factory.CreateAuthenticatedClientAsync(OwnerEmail, Password);

        (await t1.GetAsync($"/api/v1/products/{t2Product}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Product_Without_Permission_Is_Forbidden()
    {
        var factory = new AdminApiFactory();
        await using var _ = factory;
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedPlainUserAsync(1, "plain@t1.com", Password);

        HttpClient client = await factory.CreateAuthenticatedClientAsync("plain@t1.com", Password);
        (await client.GetAsync("/api/v1/products")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
