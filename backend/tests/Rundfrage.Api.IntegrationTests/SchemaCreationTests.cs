using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Rundfrage.Api.Data;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// FR-013 / FR-013a. The connectivity check is schema-independent (it runs SELECT 1), so it
/// cannot prove that schema creation succeeded. This is that separate proof.
/// </summary>
public class SchemaCreationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private RundfrageDbContext NewContext() =>
        new(new DbContextOptionsBuilder<RundfrageDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options);

    private async Task<bool> MigrationHistoryExistsAsync()
    {
        await using var db = NewContext();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = '__EFMigrationsHistory'";
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
            "SELECT table_name FROM information_schema.tables WHERE table_schema = \'public\'");

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

        var indexes = await QueryStringsAsync(
            "SELECT indexdef FROM pg_indexes WHERE schemaname = \'public\'");

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

        var indexes = await QueryStringsAsync(
            "SELECT indexdef FROM pg_indexes WHERE schemaname = \'public\'");

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

        var columns = await QueryStringsAsync(
            "SELECT lower(column_name) FROM information_schema.columns WHERE table_schema = \'public\'");

        foreach (var forbidden in new[] { "ip", "ipaddress", "ip_address", "useragent", "user_agent", "remoteaddress" })
        {
            Assert.DoesNotContain(forbidden, columns);
        }
    }
}
