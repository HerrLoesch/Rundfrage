using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rundfrage.Api.Data;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// The three storage settings that carry requirements, read back from a connection the
/// application handed out (FR-009, FR-010, FR-012a, FR-012b).
/// </summary>
/// <remarks>
/// Deliberately asked of the application's own connection rather than one this test opens.
/// A test that opened its own connection and set the same values would prove that the settings
/// exist, not that the application applies them - and the failure it could not see is exactly
/// the one that matters: a setting present in production and missing under test, or the reverse.
/// </remarks>
public class StorageSettingsTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private static async Task<string> PragmaAsync(RundfrageDbContext db, string pragma)
    {
        // Opened through the context, not through the raw connection. Opening the connection
        // directly bypasses the application's own open path - and the first version of this test
        // did exactly that, then reported the engine's defaults back as if they were the
        // application's settings. Three of the four agreed by coincidence, so it looked right.
        await db.Database.OpenConnectionAsync();

        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"PRAGMA {pragma}";
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }

    private async Task<string> ApplicationPragmaAsync(string pragma)
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        using var client = factory.CreateClient(); // forces the host to start and migrate
        await client.GetAsync("/");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
        return await PragmaAsync(db, pragma);
    }

    [Fact]
    public async Task Write_ahead_logging_is_on()
    {
        // FR-012b: an unclean stop must leave storage readable rather than needing repair.
        Assert.Equal("wal", (await ApplicationPragmaAsync("journal_mode")).ToLowerInvariant());
    }

    [Fact]
    public async Task Every_commit_is_flushed_to_disk_before_it_is_acknowledged()
    {
        // FR-012a. The usual advice with write-ahead logging is level 1 (NORMAL), which
        // acknowledges a commit before it is durably on disk - precisely the property this
        // feature argues from, and the reason an in-memory store was rejected. 2 is FULL.
        Assert.Equal("2", await ApplicationPragmaAsync("synchronous"));
    }

    [Fact]
    public async Task A_contending_writer_waits_instead_of_failing()
    {
        // FR-009, FR-010: with one writer at a time, the second must queue rather than be
        // refused, or the response cap would hold by dropping submissions.
        var milliseconds = int.Parse(await ApplicationPragmaAsync("busy_timeout"));

        Assert.True(
            milliseconds >= 1000,
            $"busy_timeout is {milliseconds} ms; a contending writer must be given time to wait");
    }

    [Fact]
    public async Task Foreign_keys_are_enforced()
    {
        // Off by default in this storage engine, which would silently turn the cascade deletes
        // the data model relies on (FR-038) into orphaned rows.
        Assert.Equal("1", await ApplicationPragmaAsync("foreign_keys"));
    }
}
