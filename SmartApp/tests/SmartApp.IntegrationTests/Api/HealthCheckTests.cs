using System.Net;
using FluentAssertions;
using Xunit;

namespace SmartApp.IntegrationTests.Api;

/// <summary>
/// Verifies the /health probe: it is anonymous (no auth required) and reports Healthy when the
/// database is reachable (the test SQLite database is).
/// </summary>
public sealed class HealthCheckTests
{
    [Fact]
    public async Task Health_Endpoint_Is_Anonymous_And_Healthy()
    {
        await using var factory = new AdminApiFactory();
        await factory.SeedPermissionCatalogAsync();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }
}
