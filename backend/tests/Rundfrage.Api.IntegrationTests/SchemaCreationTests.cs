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
    public async Task Creates_the_three_wish_list_tables()
    {
        // 008 data-model.md. Three tables, no change to the four above.
        await using (var db = NewContext())
        {
            await DatabaseStartup.ApplyMigrationsAsync(db, NullLogger.Instance, CancellationToken.None);
        }

        var tables = await QueryStringsAsync(
            "SELECT name FROM sqlite_master WHERE type = 'table'");

        Assert.Contains("WishLists", tables);
        Assert.Contains("WishItems", tables);
        Assert.Contains("WishClaims", tables);
    }

    [Fact]
    public async Task The_wish_list_token_is_unique_and_the_claim_token_is_merely_indexed()
    {
        // The list token is a capability and must not collide (008 FR-012). The claim token is
        // deliberately NOT unique: every claim of one submission carries the same one, which is
        // the whole mechanism of the personal link (008 FR-022b, research R-2).
        await using (var db = NewContext())
        {
            await DatabaseStartup.ApplyMigrationsAsync(db, NullLogger.Instance, CancellationToken.None);
        }

        var indexes = await QueryStringsAsync(IndexDefinitions);

        Assert.Contains(indexes, i => i.Contains("UNIQUE") && i.Contains("ListToken"));
        Assert.Contains(indexes, i => i.Contains("ClaimToken"));
        Assert.DoesNotContain(indexes, i => i.Contains("UNIQUE") && i.Contains("ClaimToken"));
    }

    [Fact]
    public async Task An_item_name_cannot_be_used_twice_in_one_wish_list()
    {
        // 008 FR-008, enforced by the database rather than only by the code that checks.
        await using (var db = NewContext())
        {
            await DatabaseStartup.ApplyMigrationsAsync(db, NullLogger.Instance, CancellationToken.None);
        }

        var indexes = await QueryStringsAsync(IndexDefinitions);

        Assert.Contains(indexes, i =>
            i.Contains("UNIQUE") && i.Contains("WishListId") && i.Contains("Name"));
    }

    [Fact]
    public async Task A_claim_stores_no_identity_beyond_the_name_on_it()
    {
        // 008 Principle IV. The display name is a label; nothing beside it may identify anybody.
        await using (var db = NewContext())
        {
            await DatabaseStartup.ApplyMigrationsAsync(db, NullLogger.Instance, CancellationToken.None);
        }

        await using var db2 = NewContext();
        var connection = db2.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT lower(name) FROM pragma_table_info('WishClaims')";
        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        Assert.Equal(
            new[] { "id", "wishitemid", "displayname", "claimtoken", "submittedat" }.Order(),
            columns.Order());
    }

    [Fact]
    public async Task Creates_the_Ersteller_table()
    {
        // 009 data-model.md section 1. One table, and no change to the seven above.
        await using (var db = NewContext())
        {
            await DatabaseStartup.ApplyMigrationsAsync(db, NullLogger.Instance, CancellationToken.None);
        }

        var tables = await QueryStringsAsync(
            "SELECT name FROM sqlite_master WHERE type = 'table'");

        Assert.Contains("Creators", tables);
    }

    [Fact]
    public async Task Many_Ersteller_can_be_revoked_at_once()
    {
        // 009 research R-3. Revocation is the ABSENCE of a token, which only works if a unique
        // index tolerates many NULLs. SQLite treats them as distinct; other engines do not, and
        // the whole revocation design rests on it - so it is asserted rather than assumed.
        await using (var db = NewContext())
        {
            await DatabaseStartup.ApplyMigrationsAsync(db, NullLogger.Instance, CancellationToken.None);
        }

        await using var write = NewContext();
        write.Creators.AddRange(
            new Data.Entities.Creator { Id = Guid.CreateVersion7(), Name = "Revoked one", LinkToken = null },
            new Data.Entities.Creator { Id = Guid.CreateVersion7(), Name = "Revoked two", LinkToken = null },
            new Data.Entities.Creator { Id = Guid.CreateVersion7(), Name = "Revoked three", LinkToken = null });

        var exception = await Record.ExceptionAsync(() => write.SaveChangesAsync());

        Assert.Null(exception);
        Assert.Equal(3, await write.Creators.CountAsync(c => c.LinkToken == null));
    }

    [Fact]
    public async Task The_Ersteller_name_and_link_are_unique_and_indexed()
    {
        // FR-002: the name is enforced by the database, not only by the service, so a second
        // write path cannot quietly create the duplicate. FR-004: the token is a capability and
        // must not collide.
        await using (var db = NewContext())
        {
            await DatabaseStartup.ApplyMigrationsAsync(db, NullLogger.Instance, CancellationToken.None);
        }

        var indexes = await QueryStringsAsync(IndexDefinitions);

        Assert.Contains(indexes, i =>
            i.Contains("UNIQUE") && i.Contains("Creators") && i.Contains("Name"));
        Assert.Contains(indexes, i =>
            i.Contains("UNIQUE") && i.Contains("Creators") && i.Contains("LinkToken"));
    }

    [Fact]
    public async Task Ownership_is_indexed_on_both_things_that_can_be_owned()
    {
        // 009 research R-14. The owner filter is the hot path on every creator request and this
        // equality is its only new predicate.
        await using (var db = NewContext())
        {
            await DatabaseStartup.ApplyMigrationsAsync(db, NullLogger.Instance, CancellationToken.None);
        }

        var indexes = await QueryStringsAsync(IndexDefinitions);

        Assert.Contains(indexes, i => i.Contains("Polls") && i.Contains("CreatorId"));
        Assert.Contains(indexes, i => i.Contains("WishLists") && i.Contains("CreatorId"));
    }

    [Fact]
    public async Task An_Ersteller_stores_no_credential_and_no_contact_detail()
    {
        // 009 FR-003 and Principle IV. There is no account here and nothing to make one out of:
        // no password, no email, no telephone number, no last-seen, no usage counter. This is the
        // check that would catch somebody adding one to implement a "proper" login.
        await using (var db = NewContext())
        {
            await DatabaseStartup.ApplyMigrationsAsync(db, NullLogger.Instance, CancellationToken.None);
        }

        await using var read = NewContext();
        var connection = read.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT lower(name) FROM pragma_table_info('Creators')";
        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        Assert.Equal(
            new[] { "id", "name", "linktoken", "createdat" }.Order(),
            columns.Order());
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
