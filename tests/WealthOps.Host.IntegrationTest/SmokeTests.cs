using System.Net;
using System.Text.Json;

namespace WealthOps.Host.IntegrationTest;

public sealed class SmokeTests(HostWebAppFixture fixture) : IClassFixture<HostWebAppFixture>
{
    [Fact]
    public async Task HostBootsAndRespondsToHttp()
    {
        using var response = await fixture.HttpClient.GetAsync("/", TestContext.Current.CancellationToken);

        // GET / is deliberately unmapped: v1 maps only a health probe (ADR-001), so a 404 here
        // proves the app booted and the HTTP pipeline is alive.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HealthReportsAMigratedDatabase()
    {
        using var response = await fixture.HttpClient.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using JsonDocument payload = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        JsonElement root = payload.RootElement;
        root.GetProperty("status").GetString().ShouldBe("Healthy");

        JsonElement database = root.GetProperty("database");
        database.GetProperty("canConnect").GetBoolean().ShouldBeTrue();
        database.GetProperty("vectorExtension").GetBoolean().ShouldBeTrue();
        database.GetProperty("pendingMigrations").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task HealthDoesNotDependOnTheModelEndpoint()
    {
        // The fixture points the gateway at a closed port. The Host neither owns nor uses local
        // inference, so its health must not report unhealthy because that process is absent —
        // which is also what lets the suite pass with no model running (NFR-3, AC-9).
        using var response = await fixture.HttpClient.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
