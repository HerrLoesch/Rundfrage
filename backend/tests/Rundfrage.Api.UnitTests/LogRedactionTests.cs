using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Diagnostics;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// FR-026: no credentials or connection strings in log output, including inside exception
/// messages. This is research.md R-5 measure 4 - the measure that turns the rule from a
/// convention into something the suite enforces.
/// </summary>
public class LogRedactionTests
{
    private const string Password = "sup3r-s3cret-passw0rd";

    private const string ConnectionWithWrongPassword =
        $"Host=127.0.0.1;Port=59999;Database=rundfrage;Username=rundfrage;Password={Password};Timeout=2";

    [Fact]
    public async Task Failure_log_contains_neither_the_password_nor_the_connection_string()
    {
        await using var db = new RundfrageDbContext(
            new DbContextOptionsBuilder<RundfrageDbContext>()
                .UseNpgsql(ConnectionWithWrongPassword)
                .Options);

        var logger = new RecordingLogger<DatabaseProbe>();
        var probe = new DatabaseProbe(db, logger);

        var status = await probe.CheckAsync();

        Assert.Equal(DatabaseState.Unreachable, status.State);

        var logged = logger.AllText;
        Assert.DoesNotContain(Password, logged);
        Assert.DoesNotContain("Password=", logged);
        Assert.DoesNotContain(ConnectionWithWrongPassword, logged);
    }

    [Fact]
    public async Task Failure_log_does_not_attach_the_raw_exception()
    {
        // Npgsql exception detail is where a connection string would realistically leak.
        await using var db = new RundfrageDbContext(
            new DbContextOptionsBuilder<RundfrageDbContext>()
                .UseNpgsql(ConnectionWithWrongPassword)
                .Options);

        var logger = new RecordingLogger<DatabaseProbe>();
        var probe = new DatabaseProbe(db, logger);

        await probe.CheckAsync();

        var entry = Assert.Single(logger.Entries);
        Assert.Null(entry.Exception);
        // The failure must still be identifiable by exception type.
        Assert.Matches(@"[A-Za-z]+Exception", entry.Message);
    }
}
