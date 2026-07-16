using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SmartApp.IntegrationTests.Api;
using Xunit;

namespace SmartApp.IntegrationTests.Inventory;

/// <summary>
/// End-to-end tests for stock operations: adjustments and transfers generate movements and update
/// balances, weighted-average-cost is computed correctly, movements are append-only, negative stock
/// is rejected, and everything is tenant-isolated and authorized.
/// </summary>
public sealed class StockApiTests
{
    private const string OwnerEmail = "owner@t1.com";
    private const string Password = "P@ssw0rd!";

    private sealed record InvContext(AdminApiFactory Factory, HttpClient Client, long ProductId, long Wh1, long Wh2);

    /// <summary>Owner client + a base unit, a product, and two warehouses.</summary>
    private static async Task<InvContext> ArrangeAsync(string email = OwnerEmail, long tenantId = 1)
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
            reorderLevel = 0m,
            trackStock = true,
            customFieldsJson = (string?)null,
            units = Array.Empty<object>(),
            barcodes = Array.Empty<object>(),
            prices = Array.Empty<object>(),
        }));
        long wh1 = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/warehouses",
            new { name = "WH1", code = (string?)null, address = (string?)null, isDefault = true }));
        long wh2 = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/warehouses",
            new { name = "WH2", code = (string?)null, address = (string?)null, isDefault = false }));

        return new InvContext(factory, client, productId, wh1, wh2);
    }

    private static async Task<HttpResponseMessage> AdjustAsync(
        InvContext ctx, long warehouseId, decimal qty, decimal unitCost, string? reason = "test")
        => await ctx.Client.PostAsJsonAsync("/api/v1/stock/adjust", new
        {
            productId = ctx.ProductId,
            warehouseId,
            quantityChange = qty,
            unitCost,
            reason,
        });

    private static async Task<(decimal Qty, decimal AvgCost)> ReadBalanceAsync(InvContext ctx, long warehouseId)
    {
        JsonElement data = await ApiTestJson.DataAsync(
            await ctx.Client.GetAsync($"/api/v1/stock/balances?warehouseId={warehouseId}&productId={ctx.ProductId}"));
        JsonElement row = data.EnumerateArray().Single();
        return (row.GetProperty("qtyOnHand").GetDecimal(), row.GetProperty("avgCost").GetDecimal());
    }

    [Fact]
    public async Task Adjustment_Increases_Balance_And_Records_Movement()
    {
        InvContext ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        (await AdjustAsync(ctx, ctx.Wh1, 10m, 5m)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (decimal qty, decimal avg) = await ReadBalanceAsync(ctx, ctx.Wh1);
        qty.Should().Be(10m);
        avg.Should().Be(5m);

        JsonElement movements = await ApiTestJson.DataAsync(
            await ctx.Client.GetAsync($"/api/v1/stock/movements?productId={ctx.ProductId}"));
        movements.GetArrayLength().Should().Be(1);
        movements[0].GetProperty("quantityChange").GetDecimal().Should().Be(10m);
        movements[0].GetProperty("resultingQty").GetDecimal().Should().Be(10m);
    }

    [Fact]
    public async Task Weighted_Average_Cost_Is_Computed_On_Second_Inbound()
    {
        InvContext ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        // 10 @ 5  → avg 5. Then 10 @ 15 → avg (10*5 + 10*15)/20 = 10.
        await AdjustAsync(ctx, ctx.Wh1, 10m, 5m);
        await AdjustAsync(ctx, ctx.Wh1, 10m, 15m);

        (decimal qty, decimal avg) = await ReadBalanceAsync(ctx, ctx.Wh1);
        qty.Should().Be(20m);
        avg.Should().Be(10m);
    }

    [Fact]
    public async Task Outbound_Does_Not_Change_Average_Cost()
    {
        InvContext ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        await AdjustAsync(ctx, ctx.Wh1, 10m, 8m);   // avg 8
        await AdjustAsync(ctx, ctx.Wh1, -4m, 0m);   // sell/remove 4 → avg still 8

        (decimal qty, decimal avg) = await ReadBalanceAsync(ctx, ctx.Wh1);
        qty.Should().Be(6m);
        avg.Should().Be(8m);
    }

    [Fact]
    public async Task Adjustment_Below_Zero_Is_Rejected()
    {
        InvContext ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        await AdjustAsync(ctx, ctx.Wh1, 5m, 10m);
        HttpResponseMessage over = await AdjustAsync(ctx, ctx.Wh1, -10m, 0m);
        over.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Transfer_Moves_Quantity_Between_Warehouses_Preserving_Cost()
    {
        InvContext ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        await AdjustAsync(ctx, ctx.Wh1, 10m, 7m);   // WH1: 10 @ 7

        HttpResponseMessage transfer = await ctx.Client.PostAsJsonAsync("/api/v1/stock/transfer", new
        {
            productId = ctx.ProductId,
            fromWarehouseId = ctx.Wh1,
            toWarehouseId = ctx.Wh2,
            quantity = 4m,
            reason = "rebalance",
        });
        transfer.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (decimal q1, decimal a1) = await ReadBalanceAsync(ctx, ctx.Wh1);
        (decimal q2, decimal a2) = await ReadBalanceAsync(ctx, ctx.Wh2);
        q1.Should().Be(6m);
        q2.Should().Be(4m);
        a2.Should().Be(7m, "transfer preserves the source's average cost");

        // Two movements exist for the transfer (out + in), plus the initial adjustment = 3 total.
        JsonElement movements = await ApiTestJson.DataAsync(
            await ctx.Client.GetAsync($"/api/v1/stock/movements?productId={ctx.ProductId}"));
        movements.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task Transfer_To_Same_Warehouse_Is_Validation_Error()
    {
        InvContext ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        await AdjustAsync(ctx, ctx.Wh1, 10m, 7m);
        HttpResponseMessage transfer = await ctx.Client.PostAsJsonAsync("/api/v1/stock/transfer", new
        {
            productId = ctx.ProductId,
            fromWarehouseId = ctx.Wh1,
            toWarehouseId = ctx.Wh1,
            quantity = 1m,
            reason = (string?)null,
        });
        transfer.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestJson.ErrorCodeAsync(transfer)).Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Transfer_With_Insufficient_Source_Is_Rejected_And_Leaves_No_Partial_State()
    {
        InvContext ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        await AdjustAsync(ctx, ctx.Wh1, 3m, 7m);

        HttpResponseMessage transfer = await ctx.Client.PostAsJsonAsync("/api/v1/stock/transfer", new
        {
            productId = ctx.ProductId,
            fromWarehouseId = ctx.Wh1,
            toWarehouseId = ctx.Wh2,
            quantity = 10m,
            reason = (string?)null,
        });
        transfer.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Source unchanged; destination has no row/quantity.
        (decimal q1, decimal _unused) = await ReadBalanceAsync(ctx, ctx.Wh1);
        q1.Should().Be(3m);

        JsonElement wh2 = await ApiTestJson.DataAsync(
            await ctx.Client.GetAsync($"/api/v1/stock/balances?warehouseId={ctx.Wh2}&productId={ctx.ProductId}"));
        wh2.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Product_CostPrice_Tracks_Weighted_Average_Cost()
    {
        InvContext ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        await AdjustAsync(ctx, ctx.Wh1, 10m, 5m);
        await AdjustAsync(ctx, ctx.Wh1, 10m, 15m); // WAC → 10

        JsonElement product = await ApiTestJson.DataAsync(
            await ctx.Client.GetAsync($"/api/v1/products/{ctx.ProductId}"));
        product.GetProperty("costPrice").GetDecimal().Should().Be(10m);
    }

    [Fact]
    public async Task Stock_Is_Isolated_By_Tenant()
    {
        InvContext ctxT2 = await ArrangeAsync("owner@t2.com", tenantId: 2);
        await using var _ = ctxT2.Factory;
        await AdjustAsync(ctxT2, ctxT2.Wh1, 10m, 5m);

        // Tenant 1 owner in the same factory sees no stock/movements from tenant 2.
        await ctxT2.Factory.SeedTenantWithOwnerUserAsync(1, OwnerEmail, Password);
        HttpClient t1 = await ctxT2.Factory.CreateAuthenticatedClientAsync(OwnerEmail, Password);

        JsonElement balances = await ApiTestJson.DataAsync(await t1.GetAsync("/api/v1/stock/balances"));
        balances.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Adjust_Without_Permission_Is_Forbidden()
    {
        var factory = new AdminApiFactory();
        await using var _ = factory;
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedPlainUserAsync(1, "plain@t1.com", Password);

        HttpClient client = await factory.CreateAuthenticatedClientAsync("plain@t1.com", Password);
        HttpResponseMessage adjust = await client.PostAsJsonAsync("/api/v1/stock/adjust", new
        {
            productId = 1L,
            warehouseId = 1L,
            quantityChange = 1m,
            unitCost = 1m,
            reason = (string?)null,
        });
        adjust.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
