using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SmartApp.Shared.Constants;
using Xunit;

namespace SmartApp.IntegrationTests.Api;

/// <summary>
/// End-to-end tests for the remaining administration endpoints: Permissions listing, Tenant Settings
/// (get/update), and the Current User Profile (get, update, change password).
/// </summary>
public sealed class AdministrationMiscTests
{
    private const string OwnerEmail = "owner@t1.com";
    private const string Password = "P@ssw0rd!";

    private static async Task<(AdminApiFactory Factory, HttpClient Client)> ArrangeOwnerAsync()
    {
        var factory = new AdminApiFactory();
        await factory.SeedPermissionCatalogAsync();
        await factory.SeedTenantWithOwnerUserAsync(1, OwnerEmail, Password);
        HttpClient client = await factory.CreateAuthenticatedClientAsync(OwnerEmail, Password);
        return (factory, client);
    }

    // ---- Permissions listing ----

    [Fact]
    public async Task Get_Permissions_Returns_Full_Catalog()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage response = await client.GetAsync("/api/v1/permissions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement data = await ApiTestJson.DataAsync(response);
        data.GetArrayLength().Should().Be(PermissionCatalog.All.Count);
    }

    // ---- Tenant Settings ----

    [Fact]
    public async Task Get_Settings_Returns_Defaults_When_Unset()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage response = await client.GetAsync("/api/v1/tenant/settings");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement data = await ApiTestJson.DataAsync(response);
        data.GetProperty("currency").GetString().Should().Be("SAR");
        data.GetProperty("locale").GetString().Should().Be("ar");
    }

    [Fact]
    public async Task Update_Settings_Then_Get_Reflects_Change()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage update = await client.PutAsJsonAsync("/api/v1/tenant/settings", new
        {
            currency = "JOD",
            timeZone = "Asia/Amman",
            defaultTaxRate = 16m,
            locale = "ar-JO",
            themeJson = "{\"primary\":\"#123456\"}",
        });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage get = await client.GetAsync("/api/v1/tenant/settings");
        JsonElement data = await ApiTestJson.DataAsync(get);
        data.GetProperty("currency").GetString().Should().Be("JOD");
        data.GetProperty("timeZone").GetString().Should().Be("Asia/Amman");
        data.GetProperty("defaultTaxRate").GetDecimal().Should().Be(16m);
    }

    [Fact]
    public async Task Update_Settings_With_Invalid_Currency_Returns_Validation_Error()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage update = await client.PutAsJsonAsync("/api/v1/tenant/settings", new
        {
            currency = "TOOLONG",
            timeZone = "UTC",
            defaultTaxRate = 5m,
            locale = "ar",
            themeJson = (string?)null,
        });

        update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestJson.ErrorCodeAsync(update)).Should().Be("VALIDATION_ERROR");
    }

    // ---- Current User Profile ----

    [Fact]
    public async Task Get_My_Profile_Returns_Identity_Roles_And_Permissions()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage response = await client.GetAsync("/api/v1/profile");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement data = await ApiTestJson.DataAsync(response);

        data.GetProperty("email").GetString().Should().Be(OwnerEmail);
        data.GetProperty("roles").EnumerateArray().Select(r => r.GetString())
            .Should().Contain(RoleNames.Owner);
        data.GetProperty("permissions").GetArrayLength().Should().Be(PermissionCatalog.All.Count);
    }

    [Fact]
    public async Task Update_My_Profile_Changes_Name()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage update = await client.PutAsJsonAsync("/api/v1/profile", new
        {
            fullName = "Renamed Owner",
            phone = "0791111111",
        });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage get = await client.GetAsync("/api/v1/profile");
        (await ApiTestJson.DataAsync(get)).GetProperty("fullName").GetString().Should().Be("Renamed Owner");
    }

    [Fact]
    public async Task Change_Password_With_Wrong_Current_Is_Unauthorized()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/profile/change-password", new
        {
            currentPassword = "wrong-current",
            newPassword = "Br@ndNewPass1",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ApiTestJson.ErrorCodeAsync(response)).Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Change_Password_Succeeds_And_New_Password_Works()
    {
        (AdminApiFactory factory, HttpClient client) = await ArrangeOwnerAsync();
        await using var _ = factory;

        const string newPassword = "Br@ndNewPass1";
        HttpResponseMessage change = await client.PostAsJsonAsync("/api/v1/profile/change-password", new
        {
            currentPassword = Password,
            newPassword,
        });
        change.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The new password now authenticates.
        HttpClient fresh = await factory.CreateAuthenticatedClientAsync(OwnerEmail, newPassword);
        (await fresh.GetAsync("/api/v1/profile")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Profile_Requires_Authentication()
    {
        var factory = new AdminApiFactory();
        await using var _ = factory;
        await factory.SeedPermissionCatalogAsync();
        HttpClient client = factory.CreateClient();

        (await client.GetAsync("/api/v1/profile")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
