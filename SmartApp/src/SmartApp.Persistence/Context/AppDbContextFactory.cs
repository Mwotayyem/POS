using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SmartApp.Application.Common.Interfaces;

namespace SmartApp.Persistence.Context;

/// <summary>
/// Design-time factory used by EF Core tooling (<c>dotnet ef migrations</c>) to construct
/// an <see cref="AppDbContext"/> without booting the full application. Uses a placeholder
/// connection string — migrations are generated from the model, not from a live database.
/// See SmartApp-Architecture/03-Project-Structure.md §5.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=SmartApp_DesignTime;Trusted_Connection=True;")
            .Options;

        return new AppDbContext(options, new DesignTimeTenantProvider());
    }

    /// <summary>Inert tenant provider for design-time only — no request context exists here.</summary>
    private sealed class DesignTimeTenantProvider : ITenantProvider
    {
        public long CurrentTenantId => throw new InvalidOperationException(
            "Tenant context is not available at design time.");

        public bool IsResolved => false;
        public bool IsSystemOwner => false;
        public long? TenantIdOrNull => null;
    }
}
