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
}
