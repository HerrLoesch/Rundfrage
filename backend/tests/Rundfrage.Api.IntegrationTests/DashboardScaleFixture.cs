using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// Seeds the storage to a chosen size for the figure-6 measurement (007 research.md R-4).
/// </summary>
/// <remarks>
/// <b>Why raw SQL rather than the entity model.</b> The other scale fixtures seed through
/// <see cref="RundfrageDbContext"/> because their largest case is 100,000 rows
/// (<see cref="ScaleTests"/>). FR-028c's case is 50,000,000, and EF Core change tracking cannot
/// reach that: 50 million tracked entities exhaust memory long before they reach the file. The
/// rows are therefore generated inside SQLite by recursive CTEs, which never materialises them
/// in the test process at all.
/// <para>
/// <b>Identifiers are printed, not minted.</b> Guids are stored as TEXT (see the initial
/// migration), so a deterministic 36-character spelling is all that is required, and deriving it
/// from the loop counters means the CTE needs no round trip per row. The second group of each
/// identifier discriminates the table, so a poll, a day and a response can never collide.
/// </para>
/// <para>
/// Tokens are not real capability tokens. Nothing in this fixture is ever reached through a link:
/// the measurement reads the aggregate directly, and minting 500,000 genuine tokens would cost
/// more than the query being measured.
/// </para>
/// </remarks>
public static class DashboardScaleFixture
{
    /// <summary>What one seeded shape contains, for the assertions and the report.</summary>
    public sealed record Shape(int Polls, int ResponsesPerPoll, int DaysPerPoll)
    {
        public long DayAnswers => (long)Polls * ResponsesPerPoll * DaysPerPoll;
    }

    public static void Seed(RundfrageDbContext db, Shape shape)
    {
        // Durability is irrelevant to a throwaway measurement database, and leaving the journal
        // on makes seeding the dominant cost of the test rather than an incidental one.
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=OFF;");
        db.Database.ExecuteSqlRaw("PRAGMA synchronous=OFF;");

        // Deadlines are far in the future so every seeded poll is live: the figures are all
        // scoped by RetentionService.LivePolls(), and an expired poll would be excluded and
        // measure nothing (data-model.md).
        db.Database.ExecuteSql($"""
            WITH RECURSIVE p(i) AS (
                SELECT 0 UNION ALL SELECT i + 1 FROM p WHERE i + 1 < {shape.Polls}
            )
            INSERT INTO Polls (Id, Title, Message, ParticipantToken, CreatedAt, RetentionDeadline)
            SELECT
                printf('%08x-0001-0000-0000-%012x', i, i),
                'Terminfindung ' || i,
                NULL,
                printf('%022d', i),
                '2026-01-01 00:00:00',
                '2099-01-01 00:00:00'
            FROM p;
            """);

        db.Database.ExecuteSql($"""
            WITH RECURSIVE d(j) AS (
                SELECT 0 UNION ALL SELECT j + 1 FROM d WHERE j + 1 < {shape.DaysPerPoll}
            )
            INSERT INTO CandidateDays (Id, PollId, Date)
            SELECT
                printf('%08x-0002-0000-%04x-%012x', p.rowid - 1, d.j, d.j),
                p.Id,
                date('2027-01-01', '+' || d.j || ' day')
            FROM Polls p, d;
            """);

        db.Database.ExecuteSql($"""
            WITH RECURSIVE r(k) AS (
                SELECT 0 UNION ALL SELECT k + 1 FROM r WHERE k + 1 < {shape.ResponsesPerPoll}
            )
            INSERT INTO Responses (Id, PollId, DisplayName, EditToken, SubmittedAt)
            SELECT
                printf('%08x-0003-0000-%04x-%012x', p.rowid - 1, r.k, r.k),
                p.Id,
                'Person ' || r.k,
                printf('%011d%011d', p.rowid - 1, r.k),
                '2026-06-01 00:00:00'
            FROM Polls p, r;
            """);

        // Every response answers every day, which is what makes this the worst case: it is the
        // only shape that produces Polls x Responses x Days rows. Availability cycles through all
        // three values so no group is empty and the grouped count has three buckets to fill.
        db.Database.ExecuteSqlRaw("""
            INSERT INTO DayAnswers (ResponseId, CandidateDayId, Availability)
            SELECT r.Id, c.Id, 1 + ((r.rowid + c.rowid) % 3)
            FROM Responses r
            JOIN CandidateDays c ON c.PollId = r.PollId;
            """);
    }

    /// <summary>Bytes on disk, including the journal companions if they are present.</summary>
    public static long StorageBytes(string dataDirectory) =>
        new DirectoryInfo(dataDirectory).Exists
            ? new DirectoryInfo(dataDirectory)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length)
            : 0;
}
