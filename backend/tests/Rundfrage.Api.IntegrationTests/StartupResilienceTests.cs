using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Rundfrage.Api.Data;
using Rundfrage.Api.Security;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// FR-024: storage that cannot be reached must cost the requests that need it, not the
/// application. Applying the schema at startup must never turn "storage unavailable" into
/// "nothing responds" - a conventional MigrateAsync() would throw and kill the host.
/// </summary>
public class StartupResilienceTests
{
    [Fact]
    public async Task The_host_starts_and_serves_when_storage_cannot_be_reached()
    {
        using var factory = new ApiFactory(ApiFactory.UnreachableDirectory);
        var client = factory.CreateClient();

        // A route that needs no storage at all. Sign-in is configuration, not data (002 FR-045),
        // so refusing here would mean the outage had spread beyond what it touches.
        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/session", new { user = "nobody", password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_page_shell_is_still_served_when_storage_cannot_be_reached()
    {
        using var factory = new ApiFactory(ApiFactory.UnreachableDirectory);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task An_answer_is_never_accepted_while_storage_is_unreachable()
    {
        // The specification's edge case, and the half that matters most: an application that
        // stays up is only an improvement if it stops pretending. Telling a participant their
        // answer was recorded when nothing was written is worse than being unavailable.
        using var factory = new ApiFactory(ApiFactory.UnreachableDirectory);

        var response = await factory.CreateClient().PostAsJsonAsync(
            $"/api/v1/polls/{CapabilityToken.Mint()}/responses",
            new { displayName = "Niemand", answers = Array.Empty<object>() });

        Assert.NotEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.False(response.IsSuccessStatusCode, "an unrecorded answer must not be confirmed");
    }

    [Fact]
    public async Task A_schema_failure_is_reported_rather_than_thrown()
    {
        await using var db = new RundfrageDbContext(
            new DbContextOptionsBuilder<RundfrageDbContext>()
                .UseSqlite(StorageLocation.ConnectionStringFor(ApiFactory.UnreachableDirectory))
                .Options);

        var applied = await DatabaseStartup.ApplyMigrationsAsync(
            db, NullLogger.Instance, CancellationToken.None, maxAttempts: 2, baseDelay: TimeSpan.FromMilliseconds(50));

        Assert.False(applied);
    }
}
