using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Rundfrage.Api.Data;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// FR-013 / FR-013a. The connectivity check is schema-independent (it runs SELECT 1), so it
/// cannot prove that schema creation succeeded. This is that separate proof.
/// </summary>
public class SchemaCreationTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private RundfrageDbContext NewContext() =>
        new(new DbContextOptionsBuilder<RundfrageDbContext>()
            .UseSqlite(StorageLocation.ConnectionStringFor(storage.DataDirectory))
            .Options);

    private async Task<bool> MigrationHistoryExistsAsync()
    {
        await using var db = NewContext();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory'";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync());
        return count > 0;
    }

    [Fact]
    public async Task Creates_the_schema_on_first_start_against_an_empty_database()
    {
        await using (var db = NewContext())
        {
            await DatabaseStartup.ApplyMigrationsAsync(db, NullLogger.Instance, CancellationToken.None);
        }

        Assert.True(
            await MigrationHistoryExistsAsync(),
            "__EFMigrationsHistory must exist after startup (FR-013)");

        await using var verify = NewContext();
        var applied = await verify.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(applied);
    }

    [Fact]
    public async Task The_storage_file_appears_by_itself_in_an_empty_directory()
    {
        // FR-004: no manual step. The fixture hands over an empty directory, so the file must
        // not be there before - a test that only checked "exists afterwards" would pass against
        // a file some earlier test left behind.
        var file = StorageLocation.FileIn(storage.DataDirectory);

        if (File.Exists(file))
        {
            File.Delete(file);
        }

        await using (var db = NewContext())
        {
            await DatabaseStartup.ApplyMigrationsAsync(db, NullLogger.Instance, CancellationToken.None);
        }

        Assert.True(File.Exists(file), "the storage file must be created on first start (FR-004)");
    }

    [Fact]
    public async Task Running_again_against_an_existing_database_is_a_safe_no_op()
    {
        // Edge case "second start on an existing database".
        await using (var first = NewContext())
        {
            await DatabaseStartup.ApplyMigrationsAsync(first, NullLogger.Instance, CancellationToken.None);
        }

        await using var second = NewContext();
        var before = (await second.Database.GetAppliedMigrationsAsync()).ToArray();

        var exception = await Record.ExceptionAsync(() =>
            DatabaseStartup.ApplyMigrationsAsync(second, NullLogger.Instance, CancellationToken.None));

        Assert.Null(exception);
        var after = (await second.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal(before, after);
    }

    /// <summary>
    /// The storage keeps the statement that created each index, so uniqueness and the columns
    /// covered can be read straight off it - the same two facts the previous catalogue query
    /// produced, from a different place.
    /// </summary>
    private const string IndexDefinitions =
        "SELECT sql FROM sqlite_master WHERE type = 'index' AND sql IS NOT NULL";

    /// <summary>Every column of every table, lower-cased.</summary>
    private const string ColumnNames =
        "SELECT lower(c.name) FROM sqlite_master m JOIN pragma_table_info(m.name) c "
        + "WHERE m.type = 'table' AND m.name NOT LIKE 'sqlite_%'";

    private async Task<HashSet<string>> QueryStringsAsync(string sql)
    {
        await using var db = NewContext();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }

    [Fact]
    public async Task Creates_the_four_date_poll_tables()
    {
        await using (var db = NewContext())
        {
            await DatabaseStartup.ApplyMigrationsAsync(db, NullLogger.Instance, CancellationToken.None);
        }

        var tables = await QueryStringsAsync(
            "SELECT name FROM sqlite_master WHERE type = 'table'");

        Assert.Contains("Polls", tables);
        Assert.Contains("CandidateDays", tables);
        Assert.Contains("Responses", tables);
        Assert.Contains("DayAnswers", tables);
    }

    [Fact]
    public async Task Both_capability_tokens_are_unique_and_indexed()
    {
        // Under Principle I the token is the authorisation, so a collision would hand one
        // person another person's capability (FR-017).
        await using (var db = NewContext())
        {
            await DatabaseStartup.ApplyMigrationsAsync(db, NullLogger.Instance, CancellationToken.None);
        }

        var indexes = await QueryStringsAsync(IndexDefinitions);

        Assert.Contains(indexes, i => i.Contains("UNIQUE") && i.Contains("ParticipantToken"));
        Assert.Contains(indexes, i => i.Contains("UNIQUE") && i.Contains("EditToken"));
    }

    [Fact]
    public async Task A_day_cannot_be_added_twice_to_the_same_poll()
    {
        // FR-012, enforced by the database rather than only by the code that de-duplicates.
        await using (var db = NewContext())
        {
            await DatabaseStartup.ApplyMigrationsAsync(db, NullLogger.Instance, CancellationToken.None);
        }

        var indexes = await QueryStringsAsync(IndexDefinitions);

        Assert.Contains(indexes, i =>
            i.Contains("UNIQUE") && i.Contains("PollId") && i.Contains("Date"));
    }

    [Fact]
    public async Task No_table_stores_a_request_source()
    {
        // FR-042 and SC-021. This is the check that would catch someone adding an IP column to
        // implement duplicate prevention - the route Principle I forbids.
        await using (var db = NewContext())
        {
            await DatabaseStartup.ApplyMigrationsAsync(db, NullLogger.Instance, CancellationToken.None);
        }

        var columns = await QueryStringsAsync(ColumnNames);

        foreach (var forbidden in new[] { "ip", "ipaddress", "ip_address", "useragent", "user_agent", "remoteaddress" })
        {
            Assert.DoesNotContain(forbidden, columns);
        }
    }
}
