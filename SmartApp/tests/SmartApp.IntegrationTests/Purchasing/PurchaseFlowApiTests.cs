using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SmartApp.IntegrationTests.Api;
using Xunit;

namespace SmartApp.IntegrationTests.Purchasing;

/// <summary>
/// End-to-end tests for the purchasing flow: a purchase invoice receives stock (updating WAC) and
/// increases the supplier balance; a purchase return reverses both; the purchase-order status
/// workflow; over-return prevention; tenant isolation and authorization.
/// </summary>
public sealed class PurchaseFlowApiTests
{
    private const string OwnerEmail = "owner@t1.com";
    private const string Password = "P@ssw0rd!";

    private sealed record Ctx(
        AdminApiFactory Factory, HttpClient Client, long ProductId, long WarehouseId, long SupplierId);

    private static async Task<Ctx> ArrangeAsync(string email = OwnerEmail, long tenantId = 1)
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
        long warehouseId = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/warehouses",
            new { name = "Main", code = (string?)null, address = (string?)null, isDefault = true }));
        long supplierId = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/suppliers",
            new { name = "Supplier A", phone = (string?)null, email = (string?)null, address = (string?)null }));

        return new Ctx(factory, client, productId, warehouseId, supplierId);
    }

    private static object InvoiceBody(Ctx ctx, decimal qty, decimal unitPrice, decimal paid = 0m, decimal taxRate = 0m)
        => new
        {
            supplierId = ctx.SupplierId,
            warehouseId = ctx.WarehouseId,
            invoiceDate = (DateTime?)null,
            paidAmount = paid,
            notes = (string?)null,
            purchaseOrderId = (long?)null,
            items = new[] { new { productId = ctx.ProductId, quantity = qty, unitPrice, discountAmount = 0m, taxRate } },
        };

    private static async Task<(decimal Qty, decimal AvgCost)> ReadBalanceAsync(Ctx ctx)
    {
        JsonElement data = await ApiTestJson.DataAsync(await ctx.Client.GetAsync(
            $"/api/v1/stock/balances?warehouseId={ctx.WarehouseId}&productId={ctx.ProductId}"));
        JsonElement row = data.EnumerateArray().Single();
        return (row.GetProperty("qtyOnHand").GetDecimal(), row.GetProperty("avgCost").GetDecimal());
    }

    private static async Task<decimal> ReadSupplierBalanceAsync(Ctx ctx)
    {
        JsonElement data = await ApiTestJson.DataAsync(await ctx.Client.GetAsync($"/api/v1/suppliers/{ctx.SupplierId}"));
        return data.GetProperty("balance").GetDecimal();
    }

    [Fact]
    public async Task Purchase_Invoice_Receives_Stock_And_Updates_Balance_And_WAC()
    {
        Ctx ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        HttpResponseMessage create = await ctx.Client.PostAsJsonAsync("/api/v1/purchase-invoices",
            InvoiceBody(ctx, qty: 10m, unitPrice: 5m));
        create.StatusCode.Should().Be(HttpStatusCode.OK);

        (decimal qty, decimal avg) = await ReadBalanceAsync(ctx);
        qty.Should().Be(10m);
        avg.Should().Be(5m);

        // Nothing paid → full grand total is payable.
        (await ReadSupplierBalanceAsync(ctx)).Should().Be(50m);

        // Product cost tracks WAC.
        JsonElement product = await ApiTestJson.DataAsync(await ctx.Client.GetAsync($"/api/v1/products/{ctx.ProductId}"));
        product.GetProperty("costPrice").GetDecimal().Should().Be(5m);
    }

    [Fact]
    public async Task Second_Purchase_Recomputes_Weighted_Average_Cost()
    {
        Ctx ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        await ctx.Client.PostAsJsonAsync("/api/v1/purchase-invoices", InvoiceBody(ctx, 10m, 5m));
        await ctx.Client.PostAsJsonAsync("/api/v1/purchase-invoices", InvoiceBody(ctx, 10m, 15m));

        (decimal qty, decimal avg) = await ReadBalanceAsync(ctx);
        qty.Should().Be(20m);
        avg.Should().Be(10m); // (10*5 + 10*15)/20
    }

    [Fact]
    public async Task Partial_Paid_Invoice_Adds_Only_Unpaid_To_Balance()
    {
        Ctx ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        // 10 @ 5 = 50 grand total, 20 paid → 30 payable.
        await ctx.Client.PostAsJsonAsync("/api/v1/purchase-invoices", InvoiceBody(ctx, 10m, 5m, paid: 20m));
        (await ReadSupplierBalanceAsync(ctx)).Should().Be(30m);
    }

    [Fact]
    public async Task Purchase_Return_Reverses_Stock_And_Balance()
    {
        Ctx ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        long invoiceId = await ApiTestJson.ReadIdAsync(
            await ctx.Client.PostAsJsonAsync("/api/v1/purchase-invoices", InvoiceBody(ctx, 10m, 5m)));

        // Find the invoice line id.
        JsonElement invoice = await ApiTestJson.DataAsync(await ctx.Client.GetAsync($"/api/v1/purchase-invoices/{invoiceId}"));
        long lineId = invoice.GetProperty("items")[0].GetProperty("id").GetInt64();

        HttpResponseMessage ret = await ctx.Client.PostAsJsonAsync($"/api/v1/purchase-invoices/{invoiceId}/returns", new
        {
            returnDate = (DateTime?)null,
            reason = "damaged",
            items = new[] { new { purchaseInvoiceItemId = lineId, quantity = 4m } },
        });
        ret.StatusCode.Should().Be(HttpStatusCode.OK);

        // Stock 10 → 6; supplier balance 50 → 30 (returned 4 @ 5 = 20).
        (decimal qty, decimal _avg) = await ReadBalanceAsync(ctx);
        qty.Should().Be(6m);
        (await ReadSupplierBalanceAsync(ctx)).Should().Be(30m);

        // Invoice now PartiallyReturned (status 3).
        JsonElement after = await ApiTestJson.DataAsync(await ctx.Client.GetAsync($"/api/v1/purchase-invoices/{invoiceId}"));
        after.GetProperty("status").GetByte().Should().Be(3);
        after.GetProperty("items")[0].GetProperty("returnedQty").GetDecimal().Should().Be(4m);
    }

    [Fact]
    public async Task Over_Return_Is_Rejected()
    {
        Ctx ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        long invoiceId = await ApiTestJson.ReadIdAsync(
            await ctx.Client.PostAsJsonAsync("/api/v1/purchase-invoices", InvoiceBody(ctx, 5m, 5m)));
        JsonElement invoice = await ApiTestJson.DataAsync(await ctx.Client.GetAsync($"/api/v1/purchase-invoices/{invoiceId}"));
        long lineId = invoice.GetProperty("items")[0].GetProperty("id").GetInt64();

        HttpResponseMessage ret = await ctx.Client.PostAsJsonAsync($"/api/v1/purchase-invoices/{invoiceId}/returns", new
        {
            returnDate = (DateTime?)null,
            reason = (string?)null,
            items = new[] { new { purchaseInvoiceItemId = lineId, quantity = 10m } },
        });
        ret.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestJson.ErrorCodeAsync(ret)).Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Purchase_Order_Workflow_Confirm_And_Cancel()
    {
        Ctx ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        long orderId = await ApiTestJson.ReadIdAsync(await ctx.Client.PostAsJsonAsync("/api/v1/purchase-orders", new
        {
            supplierId = ctx.SupplierId,
            orderDate = (DateTime?)null,
            notes = (string?)null,
            items = new[] { new { productId = ctx.ProductId, quantity = 10m, unitCost = 5m } },
        }));

        // Draft (1) → Confirmed (2).
        (await ctx.Client.PostAsync($"/api/v1/purchase-orders/{orderId}/confirm", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        JsonElement confirmed = await ApiTestJson.DataAsync(await ctx.Client.GetAsync($"/api/v1/purchase-orders/{orderId}"));
        confirmed.GetProperty("status").GetByte().Should().Be(2);

        // Confirmed → Cancelled (4).
        (await ctx.Client.PostAsync($"/api/v1/purchase-orders/{orderId}/cancel", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        JsonElement cancelled = await ApiTestJson.DataAsync(await ctx.Client.GetAsync($"/api/v1/purchase-orders/{orderId}"));
        cancelled.GetProperty("status").GetByte().Should().Be(4);

        // Cannot confirm a cancelled order.
        (await ctx.Client.PostAsync($"/api/v1/purchase-orders/{orderId}/confirm", null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Purchase_Invoice_From_Order_Marks_It_Received()
    {
        Ctx ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        long orderId = await ApiTestJson.ReadIdAsync(await ctx.Client.PostAsJsonAsync("/api/v1/purchase-orders", new
        {
            supplierId = ctx.SupplierId,
            orderDate = (DateTime?)null,
            notes = (string?)null,
            items = new[] { new { productId = ctx.ProductId, quantity = 10m, unitCost = 5m } },
        }));

        var invoiceBody = new
        {
            supplierId = ctx.SupplierId,
            warehouseId = ctx.WarehouseId,
            invoiceDate = (DateTime?)null,
            paidAmount = 0m,
            notes = (string?)null,
            purchaseOrderId = (long?)orderId,
            items = new[] { new { productId = ctx.ProductId, quantity = 10m, unitPrice = 5m, discountAmount = 0m, taxRate = 0m } },
        };
        (await ctx.Client.PostAsJsonAsync("/api/v1/purchase-invoices", invoiceBody)).StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement order = await ApiTestJson.DataAsync(await ctx.Client.GetAsync($"/api/v1/purchase-orders/{orderId}"));
        order.GetProperty("status").GetByte().Should().Be(3); // Received
    }

    [Fact]
    public async Task Purchase_Invoice_With_No_Items_Is_Validation_Error()
    {
        Ctx ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        HttpResponseMessage create = await ctx.Client.PostAsJsonAsync("/api/v1/purchase-invoices", new
        {
            supplierId = ctx.SupplierId,
            warehouseId = ctx.WarehouseId,
            invoiceDate = (DateTime?)null,
            paidAmount = 0m,
            notes = (string?)null,
            purchaseOrderId = (long?)null,
            items = Array.Empty<object>(),
        });
        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestJson.ErrorCodeAsync(create)).Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Purchase_Invoices_Are_Isolated_By_Tenant()
    {
        Ctx ctxT2 = await ArrangeAsync("owner@t2.com", tenantId: 2);
        await using var _ = ctxT2.Factory;
        await ctxT2.Client.PostAsJsonAsync("/api/v1/purchase-invoices", InvoiceBody(ctxT2, 10m, 5m));

        await ctxT2.Factory.SeedTenantWithOwnerUserAsync(1, OwnerEmail, Password);
        HttpClient t1 = await ctxT2.Factory.CreateAuthenticatedClientAsync(OwnerEmail, Password);

        JsonElement list = await ApiTestJson.DataAsync(await t1.GetAsync("/api/v1/purchase-invoices"));
        list.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Create_Purchase_Invoice_Without_Permission_Is_Forbidden()
    {
        var factory = new AdminApiFactory();
        await using var _ = factory;
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedPlainUserAsync(1, "plain@t1.com", Password);

        HttpClient client = await factory.CreateAuthenticatedClientAsync("plain@t1.com", Password);
        HttpResponseMessage create = await client.PostAsJsonAsync("/api/v1/purchase-invoices", new
        {
            supplierId = 1L,
            warehouseId = 1L,
            invoiceDate = (DateTime?)null,
            paidAmount = 0m,
            notes = (string?)null,
            purchaseOrderId = (long?)null,
            items = new[] { new { productId = 1L, quantity = 1m, unitPrice = 1m, discountAmount = 0m, taxRate = 0m } },
        });
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
