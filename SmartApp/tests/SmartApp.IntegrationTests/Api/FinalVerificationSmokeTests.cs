using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace SmartApp.IntegrationTests.Api;

/// <summary>
/// Single consolidated end-to-end smoke test that walks the exact business chain requested for the
/// final verification, over the real HTTP → authorization → MediatR → EF Core pipeline:
/// login → create Product → create Customer → create Purchase Invoice (receives stock) →
/// create Sales Invoice (issues stock, opens receivable) → Dashboard reflects the activity.
/// Every step asserts the HTTP result so a regression in any link fails loudly.
/// </summary>
public sealed class FinalVerificationSmokeTests
{
    private const string OwnerEmail = "owner@smoke.com";
    private const string Password = "P@ssw0rd!";

    private readonly ITestOutputHelper _output;

    public FinalVerificationSmokeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Full_Business_Chain_Works_End_To_End()
    {
        var factory = new AdminApiFactory();
        await using var _ = factory;

        // 1) Auth: seed the catalog + an Owner user, then log in for a real JWT.
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedTenantWithOwnerUserAsync(1, OwnerEmail, Password);
        HttpClient client = await factory.CreateAuthenticatedClientAsync(OwnerEmail, Password);
        _output.WriteLine("[OK] Login succeeded and bearer token attached.");

        // 2) Catalog prerequisites: a unit, then a product.
        long unitId = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/units",
            new { name = "Piece", symbol = "pc", precision = (byte)0 }));

        HttpResponseMessage productResponse = await client.PostAsJsonAsync("/api/v1/products", new
        {
            name = "Smoke Widget",
            sku = "SMOKE-1",
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
        });
        productResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        long productId = await ApiTestJson.ReadIdAsync(productResponse);
        _output.WriteLine($"[OK] Product created (id={productId}).");

        // Warehouse + supplier are needed to receive stock.
        long warehouseId = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/warehouses",
            new { name = "Main", code = (string?)null, address = (string?)null, isDefault = true }));
        long supplierId = await ApiTestJson.ReadIdAsync(await client.PostAsJsonAsync("/api/v1/suppliers",
            new { name = "Supplier A", phone = (string?)null, email = (string?)null, address = (string?)null }));

        // 3) Customer.
        HttpResponseMessage customerResponse = await client.PostAsJsonAsync("/api/v1/customers",
            new { name = "Customer A", phone = (string?)null, email = (string?)null, address = (string?)null, creditLimit = 100000m });
        customerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        long customerId = await ApiTestJson.ReadIdAsync(customerResponse);
        _output.WriteLine($"[OK] Customer created (id={customerId}).");

        // 4) Purchase Invoice: receive 10 @ 6 → stock 10, WAC 6, payable 60.
        HttpResponseMessage purchase = await client.PostAsJsonAsync("/api/v1/purchase-invoices", new
        {
            supplierId,
            warehouseId,
            invoiceDate = (DateTime?)null,
            paidAmount = 0m,
            notes = (string?)null,
            purchaseOrderId = (long?)null,
            items = new[] { new { productId, quantity = 10m, unitPrice = 6m, discountAmount = 0m, taxRate = 0m } },
        });
        purchase.StatusCode.Should().Be(HttpStatusCode.OK);
        long purchaseInvoiceId = await ApiTestJson.ReadIdAsync(purchase);
        _output.WriteLine($"[OK] Purchase invoice created (id={purchaseInvoiceId}); stock received 10 @ 6.");

        // 5) Sales Invoice: sell 4 @ 20 → stock 6, receivable 80, cost-of-sale snapshot 6.
        HttpResponseMessage sale = await client.PostAsJsonAsync("/api/v1/sales-invoices", new
        {
            customerId = (long?)customerId,
            warehouseId,
            invoiceDate = (DateTime?)null,
            paidAmount = 0m,
            notes = (string?)null,
            items = new[] { new { productId, quantity = 4m, unitPrice = 20m, discountAmount = 0m, taxRate = 0m } },
        });
        sale.StatusCode.Should().Be(HttpStatusCode.OK);
        long salesInvoiceId = await ApiTestJson.ReadIdAsync(sale);
        _output.WriteLine($"[OK] Sales invoice created (id={salesInvoiceId}); stock issued 4 @ 20.");

        // Stock balance is now 6.
        JsonElement balances = await ApiTestJson.DataAsync(await client.GetAsync(
            $"/api/v1/stock/balances?warehouseId={warehouseId}&productId={productId}"));
        balances.EnumerateArray().Single().GetProperty("qtyOnHand").GetDecimal().Should().Be(6m);

        // 6) Dashboard reflects the activity.
        JsonElement dashboard = await ApiTestJson.DataAsync(await client.GetAsync("/api/v1/dashboard/summary"));
        dashboard.GetProperty("salesTotal").GetDecimal().Should().Be(80m);
        dashboard.GetProperty("purchasesTotal").GetDecimal().Should().Be(60m);
        dashboard.GetProperty("grossProfit").GetDecimal().Should().Be(56m);        // (20-6)*4
        dashboard.GetProperty("outstandingReceivables").GetDecimal().Should().Be(80m);
        dashboard.GetProperty("outstandingPayables").GetDecimal().Should().Be(60m);
        dashboard.GetProperty("salesInvoiceCount").GetInt32().Should().Be(1);
        dashboard.GetProperty("purchaseInvoiceCount").GetInt32().Should().Be(1);
        _output.WriteLine("[OK] Dashboard summary reflects sales=80, purchases=60, profit=56, A/R=80, A/P=60.");
        _output.WriteLine("[PASS] Full business chain verified end-to-end.");
    }
}
