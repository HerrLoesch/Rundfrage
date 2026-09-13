using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Xunit.Abstractions;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 007 SC-011 and research.md R-4: the dashboard's yes/maybe/no aggregate has to touch every
/// DayAnswer row, and SC-011 allows two seconds. This measures the cost before anything is built
/// on top of the assumption that it fits.
/// </summary>
/// <remarks>
/// <b>Measured 2026-09-12, and the criterion moved as a result.</b> The cost is linear in the
/// number of day-answer rows - about 0.27 s and 219 MiB per million - so FR-028c's original worst
/// case of 50,000,000 rows extrapolated to roughly 14 seconds and 10.7 GB. SC-011 and FR-028c were
/// amended to 500 polls holding up to 2,500,000 day-answers in total (research.md R-4, and the
/// second 2026-09-12 entry in the spec's Clarifications).
/// <para>
/// Two kinds of test live here on purpose. <see cref="The_documented_scale_stays_within_the_budget"/>
/// is the regression test for the amended SC-011 and it asserts. The linearity theory below it
/// reports through <see cref="ITestOutputHelper"/> without asserting a time, because its job is to
/// show the shape of the curve that justifies the bound - a second machine disagreeing about
/// absolute milliseconds must not turn that into a red suite.
/// </para>
/// </remarks>
[Trait("Category", "DashboardScale")]
public class DashboardScaleTests(ITestOutputHelper output)
{
    /// <summary>The query under measurement - figure 6 of FR-028, exactly as data-model.md states it.</summary>
    private static async Task<(int Yes, int Maybe, int No)> AggregateAsync(RundfrageDbContext db)
    {
        var now = DateTime.UtcNow;

        var grouped = await db.DayAnswers
            .Where(a => a.CandidateDay!.Poll!.RetentionDeadline > now)
            .GroupBy(a => a.Availability)
            .Select(g => new { Availability = g.Key, Count = g.Count() })
            .ToListAsync();

        int CountOf(Availability a) =>
            grouped.FirstOrDefault(g => g.Availability == a)?.Count ?? 0;

        return (CountOf(Availability.Yes), CountOf(Availability.Maybe), CountOf(Availability.No));
    }

    /// <summary>
    /// SC-011 as amended: the documented scale is 500 polls holding up to 2,500,000 day-answers.
    /// Seeded in the shape FR-028c names - 500 polls, 100 responses, 50 candidate days.
    /// </summary>
    [Fact]
    public async Task The_documented_scale_stays_within_the_budget()
    {
        var shape = new DashboardScaleFixture.Shape(500, 100, 50);
        Assert.Equal(2_500_000, shape.DayAnswers);

        var storage = new SqliteFixture();
        await storage.InitializeAsync();

        try
        {
            using var factory = new ApiFactory(storage.DataDirectory);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();

            DashboardScaleFixture.Seed(db, shape);

            var measured = Stopwatch.StartNew();
            var totals = await AggregateAsync(db);
            measured.Stop();

            output.WriteLine(
                $"documented scale: rows={shape.DayAnswers:N0} read={measured.Elapsed.TotalSeconds:F2}s " +
                $"disk={DashboardScaleFixture.StorageBytes(storage.DataDirectory) / 1024.0 / 1024.0:F0} MiB");

            Assert.Equal(shape.DayAnswers, (long)totals.Yes + totals.Maybe + totals.No);

            // The budget of SC-011. The aggregate is the only figure of the six that scans, so
            // measuring it alone is measuring the criterion.
            Assert.True(
                measured.Elapsed < TimeSpan.FromSeconds(2),
                $"SC-011 allows two seconds at the documented scale; the aggregate took {measured.Elapsed.TotalSeconds:F2}s");
        }
        finally
        {
            await storage.DisposeAsync();
        }
    }

    public static TheoryData<int, int, int> Scales => new()
    {
        // Polls, responses per poll, days per poll -> DayAnswer rows
        { 10, 1000, 100 },    //  1,000,000
        { 25, 1000, 100 },    //  2,500,000
        { 50, 1000, 100 },    //  5,000,000
    };

    /// <summary>
    /// Reports the cost at three row counts so the linearity behind FR-028c's bound stays visible.
    /// Deliberately asserts correctness and not time (see the remarks on this class).
    /// </summary>
    [Theory]
    [MemberData(nameof(Scales))]
    public async Task The_answer_distribution_is_measured_at_a_known_number_of_rows(
        int polls, int responsesPerPoll, int daysPerPoll)
    {
        var shape = new DashboardScaleFixture.Shape(polls, responsesPerPoll, daysPerPoll);

        // SqliteFixture is IAsyncLifetime, not IDisposable: initialised and disposed by hand
        // here because this test owns one storage directory per scale rather than per class.
        var storage = new SqliteFixture();
        await storage.InitializeAsync();

        try
        {
            using var factory = new ApiFactory(storage.DataDirectory);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();

            var seeding = Stopwatch.StartNew();
            DashboardScaleFixture.Seed(db, shape);
            seeding.Stop();

            // Measured twice: the first read pays for whatever the operating system has not yet
            // cached, the second is the steady state an operator would actually meet.
            var cold = Stopwatch.StartNew();
            var first = await AggregateAsync(db);
            cold.Stop();

            var warm = Stopwatch.StartNew();
            var second = await AggregateAsync(db);
            warm.Stop();

            var bytes = DashboardScaleFixture.StorageBytes(storage.DataDirectory);

            output.WriteLine(
                $"rows={shape.DayAnswers,12:N0}  seed={seeding.Elapsed.TotalSeconds,7:F1}s  " +
                $"cold={cold.Elapsed.TotalSeconds,6:F2}s  warm={warm.Elapsed.TotalSeconds,6:F2}s  " +
                $"disk={bytes / 1024.0 / 1024.0,8:F0} MiB");

            // Correctness at every scale, not only speed: the three groups must sum to every
            // seeded row, which is what FR-028a requires of the aggregate.
            Assert.Equal(shape.DayAnswers, (long)first.Yes + first.Maybe + first.No);
            Assert.Equal(first, second);

            // No group may be empty, or the measurement would not be exercising three buckets.
            Assert.True(first.Yes > 0 && first.Maybe > 0 && first.No > 0);
        }
        finally
        {
            await storage.DisposeAsync();
        }
    }
}
