using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Rundfrage.Api.Data;
using Rundfrage.Api.Forms;
using Xunit.Abstractions;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 010 SC-009 and FR-047a: the forms area and one form's response list at the documented scale -
/// 200 forms, up to 50 fields each, and 200,000 responses installation-wide - within two seconds.
/// </summary>
/// <remarks>
/// Unlike 007's dashboard aggregate this needed no measurement spike before design: 200,000 rows
/// is the same order of magnitude 008's research R-12 already measured comfortably inside budget
/// for wish-list claims, and this feature's own queries (data-model.md, research.md) are equally
/// straightforward indexed lookups. Asserted anyway, because the failure this guards against is
/// shape rather than raw speed: a query per form or per response would be hundreds of round trips
/// inside one request, invisible until somebody actually reaches the documented scale.
/// </remarks>
[Trait("Category", "FormScale")]
public class FormScaleTests(ITestOutputHelper output)
{
    private const int Forms = 200;
    private const int FieldsPerForm = 50;
    private const int ResponsesPerForm = 1000;

    /// <summary>200 x 1000 = 200,000, the bound FR-047a documents.</summary>
    private const int ExpectedResponses = Forms * ResponsesPerForm;

    private static void Seed(RundfrageDbContext db)
    {
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=OFF;");
        db.Database.ExecuteSqlRaw("PRAGMA synchronous=OFF;");

        db.Database.ExecuteSql($"""
            WITH RECURSIVE f(i) AS (
                SELECT 0 UNION ALL SELECT i + 1 FROM f WHERE i + 1 < {Forms}
            )
            INSERT INTO Forms (Id, Title, FormToken, CreatedAt)
            SELECT
                printf('%08x-0031-0000-0000-%012x', i, i),
                'Formular ' || i,
                printf('%022d', i),
                '2026-01-01 00:00:00'
            FROM f;
            """);

        // 010 FR-011a: the enforced ceiling, so this is the real worst case per form rather than
        // a documented approximation of one.
        db.Database.ExecuteSql($"""
            WITH RECURSIVE l(j) AS (
                SELECT 0 UNION ALL SELECT j + 1 FROM l WHERE j + 1 < {FieldsPerForm}
            )
            INSERT INTO FormFields (Id, FormId, Type, Label, Required, MaxLength, MinLength, DisplayOrder)
            SELECT
                printf('%08x-0032-0000-%04x-%012x', fm.rowid - 1, l.j, l.j),
                fm.Id,
                'Text',
                'Feld ' || l.j,
                0,
                100,
                NULL,
                l.j
            FROM Forms fm, l;
            """);

        db.Database.ExecuteSql($"""
            WITH RECURSIVE r(k) AS (
                SELECT 0 UNION ALL SELECT k + 1 FROM r WHERE k + 1 < {ResponsesPerForm}
            )
            INSERT INTO FormResponses (Id, FormId, SubmittedAt)
            SELECT
                printf('%08x-0033-%04x-%04x-%012x', fm.rowid - 1, r.k, r.k, r.k),
                fm.Id,
                '2026-06-01 00:00:00'
            FROM Forms fm, r;
            """);

        // One value per response, on the form's first field - enough to exercise
        // FormFieldValue's indexes at the documented total without a second, unbounded axis.
        db.Database.ExecuteSql($"""
            INSERT INTO FormFieldValues (Id, ResponseId, FieldId, Value)
            SELECT
                printf('%08x-0034-0000-0000-%012x', resp.rowid - 1, resp.rowid - 1),
                resp.Id,
                (SELECT ff.Id FROM FormFields ff
                 WHERE ff.FormId = resp.FormId AND ff.DisplayOrder = 0),
                'Antwort'
            FROM FormResponses resp;
            """);
    }

    [Fact]
    public async Task The_forms_area_stays_within_the_budget_at_the_documented_scale()
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

            Assert.Equal(ExpectedResponses, await db.FormResponses.CountAsync());
            Assert.Equal(Forms * FieldsPerForm, await db.FormFields.CountAsync());

            var forms = new FormService(db, new Rundfrage.Api.Time.BerlinClock(TimeProvider.System),
                NullLogger<FormService>.Instance);

            // Warm, then measured: the first call pays for opening the file.
            await forms.ListAsync(CancellationToken.None);

            var stopwatch = Stopwatch.StartNew();
            var rows = await forms.ListAsync(CancellationToken.None);
            stopwatch.Stop();

            output.WriteLine(
                $"{Forms} forms, {ExpectedResponses} responses: {stopwatch.Elapsed.TotalSeconds:F2} s");

            Assert.Equal(Forms, rows.Count);
            Assert.All(rows, row =>
            {
                Assert.Equal(FieldsPerForm, row.FieldCount);
                Assert.Equal(ResponsesPerForm, row.ResponseCount);
            });

            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(2),
                $"SC-009 allows two seconds; the forms area took {stopwatch.Elapsed.TotalSeconds:F2} s");
        }
        finally
        {
            await storage.DisposeAsync();
        }
    }

    [Fact]
    public async Task One_forms_response_list_stays_within_the_budget_at_the_documented_scale()
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

            var forms = new FormService(db, new Rundfrage.Api.Time.BerlinClock(TimeProvider.System),
                NullLogger<FormService>.Instance);

            var oneFormId = await db.Forms.Select(f => f.Id).FirstAsync();

            await forms.ListResponsesAsync(oneFormId, CancellationToken.None);

            var stopwatch = Stopwatch.StartNew();
            var responses = await forms.ListResponsesAsync(oneFormId, CancellationToken.None);
            stopwatch.Stop();

            output.WriteLine(
                $"1 form's {ResponsesPerForm} responses: {stopwatch.Elapsed.TotalSeconds:F2} s");

            Assert.Equal(ResponsesPerForm, responses.Count);
            Assert.All(responses, r => Assert.Single(r.Values));

            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(2),
                $"SC-009 allows two seconds; one form's response list took "
                + $"{stopwatch.Elapsed.TotalSeconds:F2} s");
        }
        finally
        {
            await storage.DisposeAsync();
        }
    }
}
