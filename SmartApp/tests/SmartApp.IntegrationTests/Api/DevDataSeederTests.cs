using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Persistence.Context;
using SmartApp.Persistence.Seeding;
using Xunit;

namespace SmartApp.IntegrationTests.Api;

/// <summary>
/// Verifies the development Owner-login seeder (<see cref="DevDataSeeder"/>). On a fresh database it
/// provisions a tenant + Owner role (all permissions) + Owner user, so the default account can log in
/// over real HTTP and is authorized for a protected endpoint. Also verifies idempotency (it never
/// creates a second user). The factory disables auto-seeding so these tests drive the seeder explicitly.
/// </summary>
public sealed class DevDataSeederTests
{
    private const string OwnerEmail = "admin@smartapp.local";
    private const string OwnerPassword = "Admin@123456";

    private static async Task<DevDataSeeder.SeedOutcome> RunSeederAsync(AuthApiFactory factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // The catalog must exist first (Owner role is granted every permission).
        await PermissionSeeder.SeedAsync(db);

        return await DevDataSeeder.SeedAsync(
            db, hasher, OwnerEmail, OwnerPassword, "Demo Company", "DEMO");
    }

    [Fact]
    public async Task Seeds_A_Working_Owner_Login_On_A_Fresh_Database()
    {
        await using var factory = new AuthApiFactory();

        DevDataSeeder.SeedOutcome outcome = await RunSeederAsync(factory);
        outcome.Should().Be(DevDataSeeder.SeedOutcome.Created);

        HttpClient client = factory.CreateClient();

        // 1) Login with the default credentials succeeds over real HTTP.
        HttpResponseMessage login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = OwnerEmail, password = OwnerPassword });
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement data = await ApiTestJson.DataAsync(login);
        string token = data.GetProperty("accessToken").GetString()!;
        token.Should().NotBeNullOrWhiteSpace();

        // 2) The token authorizes a permission-guarded endpoint (Owner has all permissions).
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage users = await client.GetAsync("/api/v1/users");
        users.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Seeder_Is_Idempotent_And_Never_Creates_A_Second_User()
    {
        await using var factory = new AuthApiFactory();

        (await RunSeederAsync(factory)).Should().Be(DevDataSeeder.SeedOutcome.Created);

        // A second run must be a no-op (a user already exists).
        (await RunSeederAsync(factory)).Should().Be(DevDataSeeder.SeedOutcome.SkippedUsersExist);

        // Exactly one user exists (query across tenants — no ambient tenant in this scope).
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        int count = await db.Users.IgnoreQueryFilters().CountAsync();
        count.Should().Be(1);
    }
}
