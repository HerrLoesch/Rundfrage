using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Rundfrage.Api.Data;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// FR-011 and research.md R-2: applying migrations at startup must never turn "database down"
/// into "nothing responds". A conventional MigrateAsync() would throw and kill the host.
/// </summary>
public class StartupResilienceTests
{
    [Fact]
    public async Task Host_starts_and_serves_when_the_database_is_unreachable()
    {
        using var factory = new ApiFactory(ApiFactory.UnreachableConnection);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/message");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_page_shell_is_still_served_when_the_database_is_unreachable()
    {
        using var factory = new ApiFactory(ApiFactory.UnreachableConnection);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Migration_failure_is_reported_rather_than_thrown()
    {
        await using var db = new RundfrageDbContext(
            new DbContextOptionsBuilder<RundfrageDbContext>()
                .UseNpgsql(ApiFactory.UnreachableConnection)
                .Options);

        var applied = await DatabaseStartup.ApplyMigrationsAsync(
            db, NullLogger.Instance, CancellationToken.None, maxAttempts: 2, baseDelay: TimeSpan.FromMilliseconds(50));

        Assert.False(applied);
    }
}
