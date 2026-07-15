using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
using SmartApp.Persistence.Seeding;

namespace SmartApp.IntegrationTests.Api;

/// <summary>
/// Web factory for the administration endpoints. Like <see cref="AuthApiFactory"/> it swaps SQL Server
/// for a shared in-memory SQLite database and injects a test JWT key, but it additionally seeds the
/// permission catalog and provisions an Owner role (all permissions) so a seeded user can obtain a
/// JWT that actually authorizes the admin APIs. Exercises the real HTTP → authZ → MediatR → DbContext
/// pipeline end-to-end.
/// </summary>
public sealed class AdminApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public const string TestSigningKey = "smartapp-admin-integration-test-signing-key-should-be-long-256bit";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

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
            RemoveDbContextRegistrations(services);

            _connection.Open();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            using ServiceProvider provider = services.BuildServiceProvider();
            using IServiceScope scope = provider.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });
    }

    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
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

    /// <summary>Ensures the global permission catalog is seeded (idempotent).</summary>
    public async Task SeedPermissionCatalogAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await PermissionSeeder.SeedAsync(db);
    }

    /// <summary>
    /// Seeds an active tenant, provisions its Owner role (all permissions), and creates a user
    /// assigned to that role. Returns the created user's id. Requires the catalog to be seeded first.
    /// </summary>
    public async Task<long> SeedTenantWithOwnerUserAsync(
        long tenantId, string email, string password, TenantStatus status = TenantStatus.Active)
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
            await db.SaveChangesAsync();
        }

        long ownerRoleId = await TenantRoleSeeder.SeedOwnerRoleAsync(db, tenantId);

        var user = new AppUser
        {
            TenantId = tenantId,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            PasswordHash = hasher.Hash(password),
            FullName = "Owner User",
            IsActive = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = ownerRoleId, TenantId = tenantId });
        await db.SaveChangesAsync();

        return user.Id;
    }

    /// <summary>
    /// Seeds a plain user (no roles) for a tenant — used to prove permission checks reject a caller
    /// who lacks the required permission. Returns the user id.
    /// </summary>
    public async Task<long> SeedPlainUserAsync(
        long tenantId, string email, string password, TenantStatus status = TenantStatus.Active)
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
            await db.SaveChangesAsync();
        }

        var user = new AppUser
        {
            TenantId = tenantId,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            PasswordHash = hasher.Hash(password),
            FullName = "Plain User",
            IsActive = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>
    /// Logs in with the given credentials and returns an <see cref="HttpClient"/> with the resulting
    /// bearer access token attached to its default headers.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        HttpClient client = CreateClient();

        HttpResponseMessage login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();

        using JsonDocument doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        string token = doc.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
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
