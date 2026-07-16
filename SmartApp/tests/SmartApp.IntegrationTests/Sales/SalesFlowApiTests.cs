using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SmartApp.IntegrationTests.Api;
using Xunit;

namespace SmartApp.IntegrationTests.Sales;

/// <summary>
/// End-to-end tests for the sales flow: a sale issues stock (deducted at, and snapshotting, the
/// weighted-average cost) and increases the customer balance; a payment reduces the balance; a return
/// restocks and reduces the balance; a sale beyond available stock is rejected; plus tenant isolation
/// and authorization. Stock is first received via a purchase invoice to establish WAC.
/// </summary>
public sealed class SalesFlowApiTests
{
    private const string OwnerEmail = "owner@t1.com";
    private const string Password = "P@ssw0rd!";

    private sealed record Ctx(
        AdminApiFactory Factory, HttpClient Client, long ProductId, long WarehouseId,
        long SupplierId, long CustomerId);

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
        long customerId = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/customers",
            new { name = "Customer A", phone = (string?)null, email = (string?)null, address = (string?)null, creditLimit = 100000m }));

        return new Ctx(factory, client, productId, warehouseId, supplierId, customerId);
    }

    /// <summary>Receives <paramref name="qty"/> units at <paramref name="unitCost"/> via a purchase invoice.</summary>
    private static async Task ReceiveStockAsync(Ctx ctx, decimal qty, decimal unitCost)
    {
        var body = new
        {
            supplierId = ctx.SupplierId,
            warehouseId = ctx.WarehouseId,
            invoiceDate = (DateTime?)null,
            paidAmount = 0m,
            notes = (string?)null,
            purchaseOrderId = (long?)null,
            items = new[] { new { productId = ctx.ProductId, quantity = qty, unitPrice = unitCost, discountAmount = 0m, taxRate = 0m } },
        };
        (await ctx.Client.PostAsJsonAsync("/api/v1/purchase-invoices", body)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static object SaleBody(Ctx ctx, decimal qty, decimal unitPrice, long? customerId = null, decimal paid = 0m)
        => new
        {
            customerId = customerId ?? ctx.CustomerId,
            warehouseId = ctx.WarehouseId,
            invoiceDate = (DateTime?)null,
            paidAmount = paid,
            notes = (string?)null,
            items = new[] { new { productId = ctx.ProductId, quantity = qty, unitPrice, discountAmount = 0m, taxRate = 0m } },
        };

    private static async Task<(decimal Qty, decimal AvgCost)> ReadBalanceAsync(Ctx ctx)
    {
        JsonElement data = await ApiTestJson.DataAsync(await ctx.Client.GetAsync(
            $"/api/v1/stock/balances?warehouseId={ctx.WarehouseId}&productId={ctx.ProductId}"));
        JsonElement row = data.EnumerateArray().Single();
        return (row.GetProperty("qtyOnHand").GetDecimal(), row.GetProperty("avgCost").GetDecimal());
    }

    private static async Task<decimal> ReadCustomerBalanceAsync(Ctx ctx)
    {
        JsonElement data = await ApiTestJson.DataAsync(await ctx.Client.GetAsync($"/api/v1/customers/{ctx.CustomerId}"));
        return data.GetProperty("balance").GetDecimal();
    }

    [Fact]
    public async Task Sale_Reduces_Stock_Increases_Receivable_And_Snapshots_Cost()
    {
        Ctx ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        await ReceiveStockAsync(ctx, 10m, 6m); // stock 10 @ WAC 6

        HttpResponseMessage sale = await ctx.Client.PostAsJsonAsync("/api/v1/sales-invoices", SaleBody(ctx, 4m, 20m));
        sale.StatusCode.Should().Be(HttpStatusCode.OK);
        long invoiceId = await ApiTestJson.ReadIdAsync(sale);

        // Stock 10 → 6; avg cost unchanged at 6.
        (decimal qty, decimal avg) = await ReadBalanceAsync(ctx);
        qty.Should().Be(6m);
        avg.Should().Be(6m);

        // Receivable = grand total (4 * 20 = 80), nothing paid.
        (await ReadCustomerBalanceAsync(ctx)).Should().Be(80m);

        // The invoice line snapshots the cost (WAC 6) for profitability.
        JsonElement invoice = await ApiTestJson.DataAsync(await ctx.Client.GetAsync($"/api/v1/sales-invoices/{invoiceId}"));
        invoice.GetProperty("items")[0].GetProperty("unitCost").GetDecimal().Should().Be(6m);
        invoice.GetProperty("items")[0].GetProperty("unitPrice").GetDecimal().Should().Be(20m);
    }

    [Fact]
    public async Task Sale_Beyond_Available_Stock_Is_Rejected()
    {
        Ctx ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        await ReceiveStockAsync(ctx, 3m, 6m);
        HttpResponseMessage sale = await ctx.Client.PostAsJsonAsync("/api/v1/sales-invoices", SaleBody(ctx, 10m, 20m));
        sale.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Stock unchanged.
        (decimal qty, decimal _a) = await ReadBalanceAsync(ctx);
        qty.Should().Be(3m);
    }

    [Fact]
    public async Task Partial_Paid_Sale_Adds_Only_Unpaid_To_Receivable()
    {
        Ctx ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        await ReceiveStockAsync(ctx, 10m, 6m);
        // 4 * 20 = 80 grand total, 30 paid → 50 receivable.
        await ctx.Client.PostAsJsonAsync("/api/v1/sales-invoices", SaleBody(ctx, 4m, 20m, paid: 30m));
        (await ReadCustomerBalanceAsync(ctx)).Should().Be(50m);
    }

    [Fact]
    public async Task Payment_Reduces_Customer_Balance()
    {
        Ctx ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        await ReceiveStockAsync(ctx, 10m, 6m);
        long invoiceId = await ApiTestJson.ReadIdAsync(
            await ctx.Client.PostAsJsonAsync("/api/v1/sales-invoices", SaleBody(ctx, 4m, 20m))); // balance 80

        HttpResponseMessage pay = await ctx.Client.PostAsJsonAsync("/api/v1/payments", new
        {
            customerId = ctx.CustomerId,
            salesInvoiceId = (long?)invoiceId,
            amount = 50m,
            paymentDate = (DateTime?)null,
            method = (byte)1,
            reference = (string?)null,
            notes = (string?)null,
        });
        pay.StatusCode.Should().Be(HttpStatusCode.OK);

        (await ReadCustomerBalanceAsync(ctx)).Should().Be(30m); // 80 - 50

        // Invoice paid amount reflects the payment.
        JsonElement invoice = await ApiTestJson.DataAsync(await ctx.Client.GetAsync($"/api/v1/sales-invoices/{invoiceId}"));
        invoice.GetProperty("paidAmount").GetDecimal().Should().Be(50m);
    }

    [Fact]
    public async Task Sales_Return_Restocks_And_Reduces_Balance()
    {
        Ctx ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        await ReceiveStockAsync(ctx, 10m, 6m);
        long invoiceId = await ApiTestJson.ReadIdAsync(
            await ctx.Client.PostAsJsonAsync("/api/v1/sales-invoices", SaleBody(ctx, 4m, 20m))); // stock 6, balance 80

        JsonElement invoice = await ApiTestJson.DataAsync(await ctx.Client.GetAsync($"/api/v1/sales-invoices/{invoiceId}"));
        long lineId = invoice.GetProperty("items")[0].GetProperty("id").GetInt64();

        HttpResponseMessage ret = await ctx.Client.PostAsJsonAsync($"/api/v1/sales-invoices/{invoiceId}/returns", new
        {
            returnDate = (DateTime?)null,
            reason = "customer changed mind",
            items = new[] { new { salesInvoiceItemId = lineId, quantity = 1m } },
        });
        ret.StatusCode.Should().Be(HttpStatusCode.OK);

        // Stock 6 → 7 (1 back); balance 80 → 60 (returned 1 @ 20).
        (decimal qty, decimal _a) = await ReadBalanceAsync(ctx);
        qty.Should().Be(7m);
        (await ReadCustomerBalanceAsync(ctx)).Should().Be(60m);

        JsonElement after = await ApiTestJson.DataAsync(await ctx.Client.GetAsync($"/api/v1/sales-invoices/{invoiceId}"));
        after.GetProperty("status").GetByte().Should().Be(3); // PartiallyReturned
        after.GetProperty("items")[0].GetProperty("returnedQty").GetDecimal().Should().Be(1m);
    }

    [Fact]
    public async Task Over_Return_On_Sale_Is_Rejected()
    {
        Ctx ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        await ReceiveStockAsync(ctx, 10m, 6m);
        long invoiceId = await ApiTestJson.ReadIdAsync(
            await ctx.Client.PostAsJsonAsync("/api/v1/sales-invoices", SaleBody(ctx, 4m, 20m)));
        JsonElement invoice = await ApiTestJson.DataAsync(await ctx.Client.GetAsync($"/api/v1/sales-invoices/{invoiceId}"));
        long lineId = invoice.GetProperty("items")[0].GetProperty("id").GetInt64();

        HttpResponseMessage ret = await ctx.Client.PostAsJsonAsync($"/api/v1/sales-invoices/{invoiceId}/returns", new
        {
            returnDate = (DateTime?)null,
            reason = (string?)null,
            items = new[] { new { salesInvoiceItemId = lineId, quantity = 10m } },
        });
        ret.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestJson.ErrorCodeAsync(ret)).Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Cash_Sale_Without_Customer_Succeeds()
    {
        Ctx ctx = await ArrangeAsync();
        await using var _ = ctx.Factory;

        await ReceiveStockAsync(ctx, 10m, 6m);
        var cashSale = new
        {
            customerId = (long?)null,
            warehouseId = ctx.WarehouseId,
            invoiceDate = (DateTime?)null,
            paidAmount = 0m,
            notes = (string?)null,
            items = new[] { new { productId = ctx.ProductId, quantity = 2m, unitPrice = 20m, discountAmount = 0m, taxRate = 0m } },
        };
        (await ctx.Client.PostAsJsonAsync("/api/v1/sales-invoices", cashSale)).StatusCode.Should().Be(HttpStatusCode.OK);

        (decimal qty, decimal _a) = await ReadBalanceAsync(ctx);
        qty.Should().Be(8m);
    }

    [Fact]
    public async Task Sales_Invoices_Are_Isolated_By_Tenant()
    {
        Ctx ctxT2 = await ArrangeAsync("owner@t2.com", tenantId: 2);
        await using var _ = ctxT2.Factory;
        await ReceiveStockAsync(ctxT2, 10m, 6m);
        await ctxT2.Client.PostAsJsonAsync("/api/v1/sales-invoices", SaleBody(ctxT2, 2m, 20m));

        await ctxT2.Factory.SeedTenantWithOwnerUserAsync(1, OwnerEmail, Password);
        HttpClient t1 = await ctxT2.Factory.CreateAuthenticatedClientAsync(OwnerEmail, Password);

        JsonElement list = await ApiTestJson.DataAsync(await t1.GetAsync("/api/v1/sales-invoices"));
        list.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Create_Sales_Invoice_Without_Permission_Is_Forbidden()
    {
        var factory = new AdminApiFactory();
        await using var _ = factory;
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedPlainUserAsync(1, "plain@t1.com", Password);

        HttpClient client = await factory.CreateAuthenticatedClientAsync("plain@t1.com", Password);
        HttpResponseMessage sale = await client.PostAsJsonAsync("/api/v1/sales-invoices", new
        {
            customerId = (long?)null,
            warehouseId = 1L,
            invoiceDate = (DateTime?)null,
            paidAmount = 0m,
            notes = (string?)null,
            items = new[] { new { productId = 1L, quantity = 1m, unitPrice = 1m, discountAmount = 0m, taxRate = 0m } },
        });
        sale.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
