using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Rundfrage.Api.Data;
using Rundfrage.Api.Time;
using Rundfrage.Api.Wishes;
using Xunit.Abstractions;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 008 SC-009 and FR-054: the overview at the documented scale — 200 wish lists holding up to
/// 200,000 claims in total — within two seconds.
/// </summary>
/// <remarks>
/// Unlike 007's dashboard aggregate this needed no measurement spike before the design: 200,000
/// rows is two orders of magnitude below the 2,500,000 day-answers that measured 0.66 s there
/// (research R-12). It is asserted rather than assumed anyway, because the failure this guards
/// against is not slowness but shape: a query per list would be 200 round trips inside one
/// request, and that is invisible until somebody stores 200 lists.
/// </remarks>
[Trait("Category", "WishListScale")]
public class WishListScaleTests(ITestOutputHelper output)
{
    private const int Lists = 200;
    private const int ItemsPerList = 20;
    private const int ClaimsPerItem = 50;

    /// <summary>200 x 20 x 50 = 200,000 claims, the bound FR-054 documents.</summary>
    private const int ExpectedClaims = Lists * ItemsPerList * ClaimsPerItem;

    private static void Seed(RundfrageDbContext db)
    {
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=OFF;");
        db.Database.ExecuteSqlRaw("PRAGMA synchronous=OFF;");

        db.Database.ExecuteSql($"""
            WITH RECURSIVE l(i) AS (
                SELECT 0 UNION ALL SELECT i + 1 FROM l WHERE i + 1 < {Lists}
            )
            INSERT INTO WishLists (Id, Title, Description, TargetDate, ListToken, CreatedAt)
            SELECT
                printf('%08x-0011-0000-0000-%012x', i, i),
                'Wunschliste ' || i,
                NULL,
                -- Half open, half closed, so the ordering of FR-048c is exercised at scale too.
                CASE WHEN i % 2 = 0 THEN '2099-07-18' ELSE '2020-07-18' END,
                printf('%022d', i),
                '2026-01-01 00:00:00'
            FROM l;
            """);

        db.Database.ExecuteSql($"""
            WITH RECURSIVE t(j) AS (
                SELECT 0 UNION ALL SELECT j + 1 FROM t WHERE j + 1 < {ItemsPerList}
            )
            INSERT INTO WishItems (Id, WishListId, Name, WantedCount, Position)
            SELECT
                printf('%08x-0012-0000-%04x-%012x', w.rowid - 1, t.j, t.j),
                w.Id,
                'Sache ' || t.j,
                {ClaimsPerItem},
                t.j
            FROM WishLists w, t;
            """);

        db.Database.ExecuteSql($"""
            WITH RECURSIVE c(k) AS (
                SELECT 0 UNION ALL SELECT k + 1 FROM c WHERE k + 1 < {ClaimsPerItem}
            )
            INSERT INTO WishClaims (Id, WishItemId, DisplayName, ClaimToken, SubmittedAt)
            SELECT
                printf('%08x-0013-%04x-%04x-%012x', i.rowid - 1, c.k, c.k, c.k),
                i.Id,
                'Person ' || c.k,
                printf('%011d%011d', i.rowid - 1, c.k),
                '2026-06-01 00:00:00'
            FROM WishItems i, c;
            """);
    }

    [Fact]
    public async Task The_documented_scale_stays_within_the_budget()
    {
        var storage = new SqliteFixture();
        await storage.InitializeAsync();

        try
        {
            await using var db = new RundfrageDbContext(
                new DbContextOptionsBuilder<RundfrageDbContext>()
                    .UseSqlite(StorageLocation.ConnectionStringFor(storage.DataDirectory))
                    .Options);

            await DatabaseStartup.ApplyMigrationsAsync(db, NullLogger.Instance, CancellationToken.None);
            Seed(db);

            Assert.Equal(ExpectedClaims, await db.WishClaims.CountAsync());

            var projection = new WishListProjection(db, new BerlinClock(TimeProvider.System));

            // Warm, then measured: the first call pays for opening the file, which is not what
            // SC-009 is about.
            await projection.ListAsync(CancellationToken.None);

            var stopwatch = Stopwatch.StartNew();
            var rows = await projection.ListAsync(CancellationToken.None);
            stopwatch.Stop();

            output.WriteLine(
                $"{Lists} lists, {ExpectedClaims} claims: {stopwatch.Elapsed.TotalSeconds:F2} s");

            Assert.Equal(Lists, rows.Count);
            Assert.All(rows, row =>
            {
                Assert.Equal(ItemsPerList, row.ItemCount);
                Assert.Equal(ItemsPerList * ClaimsPerItem, row.EntryCount);
                Assert.Equal(ItemsPerList * ClaimsPerItem, row.PlaceCount);
                Assert.Equal(0, row.UntakenItemCount);
                Assert.Equal(ItemsPerList, row.CompleteItemCount);
            });

            // FR-048c holds at scale as well: every open list before every closed one.
            var firstClosed = rows.ToList().FindIndex(r => r.Closed);
            Assert.DoesNotContain(rows.Skip(firstClosed), r => !r.Closed);

            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(2),
                $"SC-009 allows two seconds; the overview took {stopwatch.Elapsed.TotalSeconds:F2} s");
        }
        finally
        {
            await storage.DisposeAsync();
        }
    }
}
