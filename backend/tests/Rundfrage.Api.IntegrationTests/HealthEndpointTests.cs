using System.Net;
using System.Net.Http.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// The endpoint the container runtime asks, and the one property that makes it useful: it
/// reports whether this process is answering, and nothing else.
/// </summary>
public class HealthEndpointTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    [Fact]
    public async Task Answers_without_a_session()
    {
        // A health check runs as nobody. Behind the admin group it would report the deployment
        // as unhealthy for the one reason that is not a health problem.
        using var factory = new ApiFactory(storage.DataDirectory);

        var response = await factory.CreateClient().GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Still_answers_when_storage_cannot_be_reached()
    {
        // The point of the whole endpoint. FR-024 keeps the application serving when its storage
        // is unreachable; a check that failed here would tell the orchestrator to replace - or
        // roll back - a container behaving exactly as the specification requires.
        using var factory = new ApiFactory(ApiFactory.UnreachableDirectory);

        var response = await factory.CreateClient().GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Discloses_nothing_beyond_that_it_is_answering()
    {
        // Unauthenticated, so every field here is public. A version, a poll count or a storage
        // path would each be something an outsider could read, added by someone who only meant
        // to make the check more informative.
        using var factory = new ApiFactory(storage.DataDirectory);

        var body = await factory.CreateClient()
            .GetFromJsonAsync<Dictionary<string, object>>("/api/v1/health");

        Assert.NotNull(body);
        Assert.Equal(new[] { "status" }, body.Keys.ToArray());
    }

    [Fact]
    public async Task Is_not_spent_from_the_submission_budget()
    {
        // FR-027a counts submissions, and the check runs every thirty seconds forever. Sharing
        // the budget would mean the container's own monitoring locked participants out.
        using var factory = new ApiFactory(storage.DataDirectory);
        var client = factory.CreateClient();

        for (var i = 0; i < RateLimitingProbeCount; i++)
        {
            var response = await client.GetAsync("/api/v1/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    /// <summary>Comfortably past FR-027a's ten per hour.</summary>
    private const int RateLimitingProbeCount = 25;
}
