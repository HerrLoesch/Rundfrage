using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Rundfrage.Api.Data;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 009 FR-013 and SC-009: everything stored before this feature survives it, and belongs to the
/// operator afterwards.
/// </summary>
/// <remarks>
/// <b>Against a populated database, not an empty one.</b> "The migration ran" and "the rows
/// survived" are different claims, and only the second one is what an operator cares about.
/// <see cref="SchemaCreationTests"/> makes the first; this makes the second.
/// <para>
/// The database is deliberately brought up to the migration <i>before</i> this feature's and then
/// filled, so the rows under test are genuinely pre-existing rows rather than rows written through
/// the new model. Anything else would prove nothing: rows inserted after the migration obviously
/// survive it.
/// </para>
/// <para>
/// The thing this guards against is a data migration that nobody needed. FR-013 is satisfied by
/// <c>CreatorId</c> being nullable - an added nullable column is already NULL on every existing
/// row, and NULL already means the operator (data-model section 3). If somebody later "improves"
/// that into a backfill, this test is what notices.
/// </para>
/// </remarks>
public sealed class MigrationOnPopulatedDatabaseTests(SqliteFixture storage)
    : IClassFixture<SqliteFixture>
{
    /// <summary>The last migration before feature 009.</summary>
    private const string BeforeThisFeature = "20260913105608_WishLists";

    private string ConnectionString => StorageLocation.ConnectionStringFor(storage.DataDirectory);

    private RundfrageDbContext NewContext() =>
        new(new DbContextOptionsBuilder<RundfrageDbContext>().UseSqlite(ConnectionString).Options);

    private async Task MigrateToAsync(string target)
    {
        await using var db = NewContext();
        await db.Database.GetInfrastructure().GetRequiredService<IMigrator>().MigrateAsync(target);
    }

    /// <summary>
    /// Writes with raw SQL rather than through the model, because the model already knows about
    /// CreatorId and the point is to produce rows written by a build that did not.
    /// </summary>
    private async Task<(Guid PollId, string PollToken, Guid WishListId, string ListToken)> SeedAsync()
    {
        var pollId = Guid.CreateVersion7();
        var dayId = Guid.CreateVersion7();
        var responseId = Guid.CreateVersion7();
        var wishListId = Guid.CreateVersion7();
        var itemId = Guid.CreateVersion7();
        var claimId = Guid.CreateVersion7();

        const string pollToken = "poll-token-before-00";
        const string editToken = "edit-token-before-00";
        const string listToken = "list-token-before-00";
        const string claimToken = "clam-token-before-00";

        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        async Task ExecuteAsync(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        var now = DateTime.UtcNow.ToString("O");
        var deadline = DateTime.UtcNow.AddDays(60).ToString("O");

        await ExecuteAsync(
            $"INSERT INTO Polls (Id, Title, Message, ParticipantToken, CreatedAt, RetentionDeadline) "
            + $"VALUES ('{pollId}', 'Vor 009 angelegt', NULL, '{pollToken}', '{now}', '{deadline}')");
        await ExecuteAsync(
            $"INSERT INTO CandidateDays (Id, PollId, Date) VALUES ('{dayId}', '{pollId}', '2026-12-01')");
        await ExecuteAsync(
            $"INSERT INTO Responses (Id, PollId, DisplayName, EditToken, SubmittedAt) "
            + $"VALUES ('{responseId}', '{pollId}', 'Alte Antwort', '{editToken}', '{now}')");
        await ExecuteAsync(
            $"INSERT INTO DayAnswers (ResponseId, CandidateDayId, Availability) "
            + $"VALUES ('{responseId}', '{dayId}', 0)");

        await ExecuteAsync(
            $"INSERT INTO WishLists (Id, Title, Description, TargetDate, ListToken, CreatedAt) "
            + $"VALUES ('{wishListId}', 'Auch vor 009', NULL, '2026-12-24', '{listToken}', '{now}')");
        await ExecuteAsync(
            $"INSERT INTO WishItems (Id, WishListId, Name, WantedCount, Position) "
            + $"VALUES ('{itemId}', '{wishListId}', 'Kuchen', 2, 0)");
        await ExecuteAsync(
            $"INSERT INTO WishClaims (Id, WishItemId, DisplayName, ClaimToken, SubmittedAt) "
            + $"VALUES ('{claimId}', '{itemId}', 'Alter Eintrag', '{claimToken}', '{now}')");

        return (pollId, pollToken, wishListId, listToken);
    }

    [Fact]
    public async Task Everything_stored_before_this_feature_survives_and_belongs_to_the_operator()
    {
        await MigrateToAsync(BeforeThisFeature);
        var seeded = await SeedAsync();

        // The step under test.
        await using (var db = NewContext())
        {
            await DatabaseStartup.ApplyMigrationsAsync(db, NullLogger.Instance, CancellationToken.None);
        }

        await using var after = NewContext();

        // Nothing lost.
        Assert.Equal(1, await after.Polls.CountAsync());
        Assert.Equal(1, await after.CandidateDays.CountAsync());
        Assert.Equal(1, await after.Responses.CountAsync());
        Assert.Equal(1, await after.DayAnswers.CountAsync());
        Assert.Equal(1, await after.WishLists.CountAsync());
        Assert.Equal(1, await after.WishItems.CountAsync());
        Assert.Equal(1, await after.WishClaims.CountAsync());

        // FR-013: owned by the operator, which is what a NULL CreatorId means (research R-2).
        var poll = await after.Polls.SingleAsync();
        var wishList = await after.WishLists.SingleAsync();
        Assert.Null(poll.CreatorId);
        Assert.Null(wishList.CreatorId);

        // FR-013 again: "behave in every respect as it did before" starts with the links still
        // being the same links. A migration that reissued a token would strand every link the
        // operator had already sent out.
        Assert.Equal(seeded.PollToken, poll.ParticipantToken);
        Assert.Equal(seeded.ListToken, wishList.ListToken);
        Assert.Equal(seeded.PollId, poll.Id);
        Assert.Equal(seeded.WishListId, wishList.Id);

        // And no Ersteller appeared from nowhere. There is no seeded "operator" row, deliberately:
        // it would need seeding, could be deleted by the endpoint FR-020a adds, and would count
        // against the hundred of FR-009 (research R-2).
        Assert.Equal(0, await after.Creators.CountAsync());
    }
}
