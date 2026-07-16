using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SmartApp.IntegrationTests.Api;
using Xunit;

namespace SmartApp.IntegrationTests.Reporting;

/// <summary>
/// End-to-end tests for the dashboard and reports: after a purchase and a sale, the summary reflects
/// totals and profit, low-stock detection works, the product report aggregates correctly, and
/// everything is tenant-isolated and authorized.
/// </summary>
public sealed class ReportingApiTests
{
    private const string OwnerEmail = "owner@t1.com";
    private const string Password = "P@ssw0rd!";

    private sealed record Ctx(AdminApiFactory Factory, HttpClient Client, long ProductId, long WarehouseId, long CustomerId);

    /// <summary>Seeds product (reorder level 5), warehouse, supplier, customer; receives 10 @ 6 and sells 4 @ 20.</summary>
    private static async Task<Ctx> ArrangeWithActivityAsync(string email = OwnerEmail, long tenantId = 1)
    {
        var factory = new AdminApiFactory();
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedTenantWithOwnerUserAsync(tenantId, email, Password);
        HttpClient client = await factory.CreateAuthenticatedClientAsync(email, Password);

        long unitId = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/units",
            new { name = "Piece", symbol = "pc", precision = (byte)0 }));
        long productId = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/products", new
        {
            name = "Widget",
            sku = "W-1",
            categoryId = (long?)null,
            brandId = (long?)null,
            baseUnitId = unitId,
            costPrice = 0m,
            salePrice = 20m,
            taxRate = 0m,
            reorderLevel = 5m,
            trackStock = true,
            customFieldsJson = (string?)null,
            units = Array.Empty<object>(),
            barcodes = Array.Empty<object>(),
            prices = Array.Empty<object>(),
        }));
        long warehouseId = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/warehouses",
            new { name = "Main", code = (string?)null, address = (string?)null, isDefault = true }));
        long supplierId = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/suppliers",
            new { name = "Supplier A", phone = (string?)null, email = (string?)null, address = (string?)null }));
        long customerId = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/customers",
            new { name = "Customer A", phone = (string?)null, email = (string?)null, address = (string?)null, creditLimit = 100000m }));

        // Receive 10 @ 6.
        await client.PostAsJsonAsync("/api/v1/purchase-invoices", new
        {
            supplierId,
            warehouseId,
            invoiceDate = (DateTime?)null,
            paidAmount = 0m,
            notes = (string?)null,
            purchaseOrderId = (long?)null,
            items = new[] { new { productId, quantity = 10m, unitPrice = 6m, discountAmount = 0m, taxRate = 0m } },
        });

        // Sell 4 @ 20 → stock 6, profit (20-6)*4 = 56.
        await client.PostAsJsonAsync("/api/v1/sales-invoices", new
        {
            customerId = (long?)customerId,
            warehouseId,
            invoiceDate = (DateTime?)null,
            paidAmount = 0m,
            notes = (string?)null,
            items = new[] { new { productId, quantity = 4m, unitPrice = 20m, discountAmount = 0m, taxRate = 0m } },
        });

        return new Ctx(factory, client, productId, warehouseId, customerId);
    }

    [Fact]
    public async Task Dashboard_Summary_Reflects_Sales_Purchases_And_Profit()
    {
        Ctx ctx = await ArrangeWithActivityAsync();
        await using var _ = ctx.Factory;

        JsonElement data = await ApiTestJson.DataAsync(await ctx.Client.GetAsync("/api/v1/dashboard/summary"));

        data.GetProperty("salesTotal").GetDecimal().Should().Be(80m);       // 4 * 20
        data.GetProperty("purchasesTotal").GetDecimal().Should().Be(60m);   // 10 * 6
        data.GetProperty("grossProfit").GetDecimal().Should().Be(56m);      // (20-6)*4
        data.GetProperty("outstandingReceivables").GetDecimal().Should().Be(80m);
        data.GetProperty("outstandingPayables").GetDecimal().Should().Be(60m);
        data.GetProperty("salesInvoiceCount").GetInt32().Should().Be(1);
        data.GetProperty("purchaseInvoiceCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Dashboard_Detects_Low_Stock()
    {
        // Reorder level 5, on-hand after selling = 6 → not low yet. Sell 2 more → 4 → low.
        Ctx ctx = await ArrangeWithActivityAsync();
        await using var _ = ctx.Factory;

        JsonElement before = await ApiTestJson.DataAsync(await ctx.Client.GetAsync("/api/v1/dashboard/summary"));
        before.GetProperty("lowStockProductCount").GetInt32().Should().Be(0);

        await ctx.Client.PostAsJsonAsync("/api/v1/sales-invoices", new
        {
            customerId = (long?)ctx.CustomerId,
            warehouseId = ctx.WarehouseId,
            invoiceDate = (DateTime?)null,
            paidAmount = 0m,
            notes = (string?)null,
            items = new[] { new { productId = ctx.ProductId, quantity = 2m, unitPrice = 20m, discountAmount = 0m, taxRate = 0m } },
        });

        JsonElement after = await ApiTestJson.DataAsync(await ctx.Client.GetAsync("/api/v1/dashboard/summary"));
        after.GetProperty("lowStockProductCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Low_Stock_Report_Lists_The_Product()
    {
        Ctx ctx = await ArrangeWithActivityAsync();
        await using var _ = ctx.Factory;

        // Drop below reorder (6 → 4).
        await ctx.Client.PostAsJsonAsync("/api/v1/sales-invoices", new
        {
            customerId = (long?)ctx.CustomerId,
            warehouseId = ctx.WarehouseId,
            invoiceDate = (DateTime?)null,
            paidAmount = 0m,
            notes = (string?)null,
            items = new[] { new { productId = ctx.ProductId, quantity = 2m, unitPrice = 20m, discountAmount = 0m, taxRate = 0m } },
        });

        JsonElement data = await ApiTestJson.DataAsync(await ctx.Client.GetAsync("/api/v1/reports/low-stock"));
        JsonElement row = data.EnumerateArray().Single();
        row.GetProperty("productId").GetInt64().Should().Be(ctx.ProductId);
        row.GetProperty("qtyOnHand").GetDecimal().Should().Be(4m);
        row.GetProperty("reorderLevel").GetDecimal().Should().Be(5m);
    }

    [Fact]
    public async Task Product_Sales_Report_Aggregates_Revenue_Cost_Profit()
    {
        Ctx ctx = await ArrangeWithActivityAsync();
        await using var _ = ctx.Factory;

        JsonElement data = await ApiTestJson.DataAsync(await ctx.Client.GetAsync("/api/v1/reports/products"));
        JsonElement row = data.EnumerateArray().Single();
        row.GetProperty("quantitySold").GetDecimal().Should().Be(4m);
        row.GetProperty("revenue").GetDecimal().Should().Be(80m);
        row.GetProperty("cost").GetDecimal().Should().Be(24m);   // 4 * 6
        row.GetProperty("profit").GetDecimal().Should().Be(56m);
    }

    [Fact]
    public async Task Sales_Report_Returns_A_Day_Row()
    {
        Ctx ctx = await ArrangeWithActivityAsync();
        await using var _ = ctx.Factory;

        JsonElement data = await ApiTestJson.DataAsync(await ctx.Client.GetAsync("/api/v1/reports/sales"));
        data.GetArrayLength().Should().BeGreaterThan(0);
        data.EnumerateArray().Sum(d => d.GetProperty("total").GetDecimal()).Should().Be(80m);
    }

    [Fact]
    public async Task Dashboard_Is_Isolated_By_Tenant()
    {
        Ctx ctxT2 = await ArrangeWithActivityAsync("owner@t2.com", tenantId: 2);
        await using var _ = ctxT2.Factory;

        await ctxT2.Factory.SeedTenantWithOwnerUserAsync(1, OwnerEmail, Password);
        HttpClient t1 = await ctxT2.Factory.CreateAuthenticatedClientAsync(OwnerEmail, Password);

        // Tenant 1 has no activity → all zeros.
        JsonElement data = await ApiTestJson.DataAsync(await t1.GetAsync("/api/v1/dashboard/summary"));
        data.GetProperty("salesTotal").GetDecimal().Should().Be(0m);
        data.GetProperty("purchasesTotal").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task Reports_Without_Permission_Are_Forbidden()
    {
        var factory = new AdminApiFactory();
        await using var _ = factory;
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedPlainUserAsync(1, "plain@t1.com", Password);

        HttpClient client = await factory.CreateAuthenticatedClientAsync("plain@t1.com", Password);
        (await client.GetAsync("/api/v1/dashboard/summary")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v1/reports/low-stock")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
