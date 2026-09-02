using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rundfrage.Api.Data;
using Rundfrage.Api.Diagnostics;

namespace Rundfrage.Api.UnitTests;

public class DatabaseProbeTests
{
    // Reserved, non-routable address: connection attempts hang rather than being refused,
    // which is what makes this a timeout test instead of a connection-refused test.
    private const string BlackholeConnection =
        "Host=10.255.255.1;Port=5432;Database=rundfrage;Username=rundfrage;Password=irrelevant;Timeout=2";

    private static RundfrageDbContext ContextFor(string connectionString) =>
        new(new DbContextOptionsBuilder<RundfrageDbContext>()
            .UseNpgsql(connectionString)
            .Options);

    [Fact]
    public async Task Reports_Unreachable_and_returns_within_the_two_second_budget()
    {
        // FR-012, SC-004a
        await using var db = ContextFor(BlackholeConnection);
        var logger = new RecordingLogger<DatabaseProbe>();
        var probe = new DatabaseProbe(db, logger);

        var stopwatch = Stopwatch.StartNew();
        var status = await probe.CheckAsync();
        stopwatch.Stop();

        Assert.Equal(DatabaseState.Unreachable, status.State);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(4),
            $"probe took {stopwatch.Elapsed.TotalSeconds:F1}s; the budget is 2s (FR-012)");
        Assert.InRange(status.DurationMs, 0, 3000);
    }

    [Fact]
    public async Task Emits_exactly_one_log_entry_carrying_outcome_and_duration()
    {
        // FR-027, SC-011
        await using var db = ContextFor(BlackholeConnection);
        var logger = new RecordingLogger<DatabaseProbe>();
        var probe = new DatabaseProbe(db, logger);

        await probe.CheckAsync();

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("nreachable", entry.Message);
        Assert.Matches(@"\d+", entry.Message);
    }

    [Fact]
    public async Task Stamps_the_moment_the_result_was_determined()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        await using var db = ContextFor(BlackholeConnection);
        var probe = new DatabaseProbe(db, new RecordingLogger<DatabaseProbe>());

        var status = await probe.CheckAsync();

        Assert.InRange(status.CheckedAt, before, DateTimeOffset.UtcNow.AddSeconds(1));
    }
}
