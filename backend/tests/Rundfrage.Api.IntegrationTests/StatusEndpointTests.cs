using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>Contract tests for GET /api/v1/status/database (contracts/openapi.yaml).</summary>
public class StatusEndpointTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Returns_200_and_reachable_when_the_database_is_up()
    {
        using var factory = new ApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/status/database");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("reachable", payload.GetProperty("state").GetString());
        Assert.True(payload.GetProperty("durationMs").GetInt32() >= 0);
        Assert.True(payload.TryGetProperty("checkedAt", out _));
    }

    [Fact]
    public async Task Returns_200_not_503_when_the_database_is_down()
    {
        // The frontend derives "backend unreachable" from any non-2xx response, so answering
        // 503 for a database outage would render as a backend outage and destroy the
        // distinction FR-010 requires (research.md R-4, contracts/openapi.yaml).
        using var factory = new ApiFactory(ApiFactory.UnreachableConnection);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/status/database");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unreachable", payload.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Response_leaks_no_internals()
    {
        // FR-014: no connection string, credential, host name, or stack trace.
        using var factory = new ApiFactory(ApiFactory.UnreachableConnection);
        var client = factory.CreateClient();

        var body = await client.GetStringAsync("/api/v1/status/database");

        Assert.DoesNotContain("10.255.255.1", body);
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Host=", body);
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", body);
    }

    [Fact]
    public async Task Response_carries_only_the_contracted_properties()
    {
        using var factory = new ApiFactory(ApiFactory.UnreachableConnection);
        var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>("/api/v1/status/database");

        var names = payload.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(["checkedAt", "durationMs", "state"], names);
    }

    [Fact]
    public async Task Each_call_performs_a_fresh_check()
    {
        // Acceptance scenario 2.4: never a cached earlier result.
        using var factory = new ApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();

        var first = await client.GetFromJsonAsync<JsonElement>("/api/v1/status/database");
        await Task.Delay(20);
        var second = await client.GetFromJsonAsync<JsonElement>("/api/v1/status/database");

        Assert.NotEqual(
            first.GetProperty("checkedAt").GetString(),
            second.GetProperty("checkedAt").GetString());
    }
}
