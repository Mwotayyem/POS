using FluentAssertions;
using SmartApp.Domain.Catalog;
using SmartApp.Domain.Inventory;
using SmartApp.Domain.Inventory.Enums;
using SmartApp.IntegrationTests.Isolation;
using SmartApp.Persistence.Context;
using Xunit;

namespace SmartApp.IntegrationTests.Inventory;

/// <summary>
/// Verifies the append-only contract for <see cref="StockMovement"/> at the persistence level: a
/// movement can be inserted, but any attempt to update or delete one is blocked by the interceptor.
/// </summary>
public sealed class AppendOnlyMovementTests
{
    /// <summary>Seeds the minimal parent rows (unit → product, warehouse) a movement's FKs require.</summary>
    private static async Task<(long ProductId, long WarehouseId)> SeedParentsAsync(TenantDbContextFactory factory)
    {
        await using var db = factory.Create();

        var unit = new Unit { TenantId = 1, Name = "pc", Precision = 0 };
        db.Units.Add(unit);
        await db.SaveChangesAsync();

        var product = new Product { TenantId = 1, Name = "P", BaseUnitId = unit.Id };
        db.Products.Add(product);

        var warehouse = new Warehouse { TenantId = 1, Name = "WH" };
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();

        return (product.Id, warehouse.Id);
    }

    private static StockMovement NewMovement(long productId, long warehouseId) => new()
    {
        TenantId = 1,
        ProductId = productId,
        WarehouseId = warehouseId,
        MovementType = StockMovementType.In,
        QuantityChange = 5m,
        UnitCost = 2m,
        ResultingQty = 5m,
        ResultingAvgCost = 2m,
        OccurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    private static async Task<long> InsertMovementAsync(TenantDbContextFactory factory, long productId, long warehouseId)
    {
        await using var db = factory.Create();
        var movement = NewMovement(productId, warehouseId);
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync();
        return movement.Id;
    }

    [Fact]
    public async Task StockMovement_Can_Be_Inserted()
    {
        using var factory = new TenantDbContextFactory();
        factory.TenantProvider.SetTenant(1);
        (long productId, long warehouseId) = await SeedParentsAsync(factory);

        await InsertMovementAsync(factory, productId, warehouseId);

        await using var verify = factory.Create();
        verify.StockMovements.Should().ContainSingle();
    }

    [Fact]
    public async Task Updating_A_StockMovement_Throws()
    {
        using var factory = new TenantDbContextFactory();
        factory.TenantProvider.SetTenant(1);
        (long productId, long warehouseId) = await SeedParentsAsync(factory);
        long id = await InsertMovementAsync(factory, productId, warehouseId);

        await using var db = factory.Create();
        var movement = await db.StockMovements.FindAsync(id);
        movement!.QuantityChange = 999m;

        Func<Task> act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*append-only*");
    }

    [Fact]
    public async Task Deleting_A_StockMovement_Throws()
    {
        using var factory = new TenantDbContextFactory();
        factory.TenantProvider.SetTenant(1);
        (long productId, long warehouseId) = await SeedParentsAsync(factory);
        long id = await InsertMovementAsync(factory, productId, warehouseId);

        await using var db = factory.Create();
        var movement = await db.StockMovements.FindAsync(id);
        db.StockMovements.Remove(movement!);

        Func<Task> act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*append-only*");
    }
}
