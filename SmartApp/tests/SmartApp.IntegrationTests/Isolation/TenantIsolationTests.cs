using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartApp.Domain.Tenancy;
using SmartApp.Persistence.Context;
using Xunit;

namespace SmartApp.IntegrationTests.Isolation;

/// <summary>
/// Proves the multi-tenant isolation foundation works end-to-end against a real relational engine:
/// the global query filter isolates tenants, TenantId is stamped server-side, and deletes are soft.
/// These are the mandatory isolation tests from SmartApp-Architecture/09-Multi-Tenant.md §8.
/// </summary>
public sealed class TenantIsolationTests : IDisposable
{
    private const long TenantA = 1001;
    private const long TenantB = 1002;

    private readonly TenantDbContextFactory _factory = new();

    public TenantIsolationTests()
    {
        // Seed the two tenants (system-owner context so no auto-stamping interferes).
        _factory.TenantProvider.IsSystemOwner = true;
        using AppDbContext ctx = _factory.Create();
        ctx.Tenants.AddRange(
            new Tenant { Id = TenantA, Name = "Tenant A", Code = "A" },
            new Tenant { Id = TenantB, Name = "Tenant B", Code = "B" });
        ctx.SaveChanges();
        _factory.TenantProvider.IsSystemOwner = false;
    }

    [Fact]
    public void TenantId_Is_Stamped_Server_Side_On_Insert()
    {
        _factory.TenantProvider.SetTenant(TenantA);
        using AppDbContext ctx = _factory.Create();

        // Note: TenantId is intentionally NOT set by the caller.
        var setting = new TenantSetting { Currency = "USD" };
        ctx.TenantSettings.Add(setting);
        ctx.SaveChanges();

        setting.TenantId.Should().Be(TenantA, "the context must stamp the current tenant on insert");
    }

    [Fact]
    public void TenantA_Cannot_Read_TenantB_Data()
    {
        // Arrange: Tenant B creates a settings row.
        _factory.TenantProvider.SetTenant(TenantB);
        using (AppDbContext ctxB = _factory.Create())
        {
            ctxB.TenantSettings.Add(new TenantSetting { Currency = "EUR" });
            ctxB.SaveChanges();
        }

        // Act: switch to Tenant A and query.
        _factory.TenantProvider.SetTenant(TenantA);
        using AppDbContext ctxA = _factory.Create();
        List<TenantSetting> visibleToA = ctxA.TenantSettings.ToList();

        // Assert: the global query filter hides Tenant B's row from Tenant A entirely.
        // Tenant A created nothing, so it must see nothing — and specifically not B's EUR row.
        visibleToA.Should().NotContain(s => s.TenantId == TenantB);
        visibleToA.Should().NotContain(s => s.Currency == "EUR");
        visibleToA.Should().BeEmpty("Tenant A created no data and must not see Tenant B's");
    }

    [Fact]
    public void Global_Query_Filter_Scopes_Reads_To_Current_Tenant()
    {
        // Each tenant inserts one row.
        _factory.TenantProvider.SetTenant(TenantA);
        using (AppDbContext ctxA = _factory.Create())
        {
            ctxA.TenantSettings.Add(new TenantSetting { Currency = "AAA" });
            ctxA.SaveChanges();
        }

        _factory.TenantProvider.SetTenant(TenantB);
        using (AppDbContext ctxB = _factory.Create())
        {
            ctxB.TenantSettings.Add(new TenantSetting { Currency = "BBB" });
            ctxB.SaveChanges();
        }

        // Tenant A sees only its own row...
        _factory.TenantProvider.SetTenant(TenantA);
        using (AppDbContext ctxA = _factory.Create())
        {
            ctxA.TenantSettings.Should().ContainSingle().Which.Currency.Should().Be("AAA");
        }

        // ...and Tenant B sees only its own row.
        _factory.TenantProvider.SetTenant(TenantB);
        using (AppDbContext ctxB = _factory.Create())
        {
            ctxB.TenantSettings.Should().ContainSingle().Which.Currency.Should().Be("BBB");
        }
    }

    [Fact]
    public void Delete_Is_Soft_And_Excluded_By_Filter()
    {
        _factory.TenantProvider.SetTenant(TenantA);

        // Create then delete a row.
        long id;
        using (AppDbContext ctx = _factory.Create())
        {
            var setting = new TenantSetting { Currency = "DEL" };
            ctx.TenantSettings.Add(setting);
            ctx.SaveChanges();
            id = setting.Id;

            ctx.TenantSettings.Remove(setting);
            ctx.SaveChanges();
        }

        using (AppDbContext ctx = _factory.Create())
        {
            // Normal query: soft-deleted row is hidden by the filter.
            ctx.TenantSettings.Any(s => s.Id == id).Should().BeFalse();

            // Bypassing the filter: the row still physically exists and is flagged deleted.
            TenantSetting? raw = ctx.TenantSettings
                .IgnoreQueryFilters()
                .FirstOrDefault(s => s.Id == id);

            raw.Should().NotBeNull();
            raw!.IsDeleted.Should().BeTrue("delete must be soft, not physical");
            raw.DeletedDate.Should().NotBeNull();
        }
    }

    public void Dispose() => _factory.Dispose();
}
