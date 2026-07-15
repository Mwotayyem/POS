using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Identity;
using SmartApp.Domain.Tenancy;
using SmartApp.Domain.Tenancy.Enums;
using SmartApp.Persistence.Context;

namespace SmartApp.IntegrationTests.Api;

/// <summary>
/// Spins up the real API in-memory (WebApplicationFactory) but replaces the SQL Server DbContext with
/// a shared in-memory SQLite database and injects a test JWT signing key. The schema is created and a
/// tenant + user are seeded once per factory. This exercises the full HTTP → middleware → MediatR →
/// DbContext pipeline end-to-end.
/// </summary>
public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public const string TestSigningKey = "smartapp-integration-test-signing-key-should-be-long-enough-256bit";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Provide test JWT settings. Added as the LAST configuration source so it overrides the
        // empty placeholders in appsettings.json regardless of source ordering.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var testSettings = new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "SmartApp",
                ["Jwt:Audience"] = "SmartApp.Clients",
                ["Jwt:SigningKey"] = TestSigningKey,
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
                ["ConnectionStrings:SmartAppDb"] = "",
            };
            config.AddInMemoryCollection(testSettings);
        });

        builder.ConfigureServices(services =>
        {
            // Remove the app's SQL Server DbContext registration and re-register on SQLite.
            RemoveDbContextRegistrations(services);

            _connection.Open();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            // Build the schema once.
            using ServiceProvider provider = services.BuildServiceProvider();
            using IServiceScope scope = provider.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });
    }

    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        // Remove the app's DbContext, its options, the options-configuration hook, and any EF Core
        // provider-internal services — otherwise both SqlServer and Sqlite providers end up registered.
        var toRemove = services
            .Where(d =>
                d.ServiceType == typeof(AppDbContext) ||
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                (d.ServiceType.FullName?.Contains("EntityFrameworkCore", StringComparison.Ordinal) ?? false) ||
                (d.ImplementationType?.FullName?.Contains("EntityFrameworkCore", StringComparison.Ordinal) ?? false))
            .ToList();

        foreach (ServiceDescriptor descriptor in toRemove)
        {
            services.Remove(descriptor);
        }
    }

    /// <summary>
    /// Seeds a tenant (with the given status) and a user (with a hashed password) for auth tests.
    /// Returns the created user id.
    /// </summary>
    public async Task<long> SeedTenantAndUserAsync(
        long tenantId, TenantStatus status, string email, string password, bool userActive = true)
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        if (!await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == tenantId))
        {
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = $"Tenant {tenantId}",
                Code = $"T{tenantId}",
                Status = status,
            });
        }

        var user = new AppUser
        {
            TenantId = tenantId,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            PasswordHash = hasher.Hash(password),
            FullName = "Test User",
            IsActive = userActive,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
