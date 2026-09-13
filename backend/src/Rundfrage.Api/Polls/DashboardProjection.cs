using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Retention;
using Rundfrage.Api.Time;

namespace Rundfrage.Api.Polls;

/// <summary>Matches the DashboardView schema in contracts/openapi.yaml.</summary>
/// <remarks>
/// Eight numbers, flat. No poll title, no participant name and no individual answer appears here -
/// FR-034 forbids it, and a flat record makes that checkable at a glance rather than requiring a
/// reader to inspect what a nested object carries.
/// <para>
/// Maintenance mode is deliberately absent. It is figure 5 of FR-028, and the admin shell already
/// reads it for the banner; carrying it here as well would create two sources for one state that
/// FR-029 requires to agree - and a banner contradicting a dashboard tile on the same screen is
/// exactly what that would produce.
/// </para>
/// </remarks>
public sealed record DashboardView(
    int PollCount,
    int ResponseCount,
    int UnansweredPolls,
    int DeletionsDueSoon,
    DateTime? NextDeletion,
    int Yes,
    int Maybe,
    int No);

/// <summary>
/// The dashboard's figures, in a fixed number of queries (FR-028b).
/// </summary>
/// <remarks>
/// <b>Fixed, not proportional.</b> Five aggregate queries run whether the installation holds three
/// polls or five hundred. Reading each poll's answers in turn is forbidden by FR-028b, and the
/// measurement behind that requirement is in research.md R-4: the distribution alone costs about
/// 0.27 s per million day-answers, so 500 reads of up to 100,000 rows each was never viable.
/// <para>
/// <b>Every figure is scoped to live polls.</b> A poll past its retention deadline is already
/// unreachable everywhere else (002 FR-039b), so counting it here would contradict the poll list
/// on the same screen. The window between a deadline passing and the hourly sweep erasing the rows
/// is precisely why the filter cannot be skipped - the rows are still on disk.
/// </para>
/// </remarks>
public sealed class DashboardProjection(
    RundfrageDbContext db, RetentionService retention, BerlinClock clock)
{
    /// <summary>How far ahead "due soon" looks (FR-028.4).</summary>
    public static readonly TimeSpan DeletionWindow = TimeSpan.FromDays(7);

    public async Task<DashboardView> BuildAsync(CancellationToken ct)
    {
        var live = retention.LivePolls();
        var windowEnd = clock.Now + DeletionWindow;

        var pollCount = await live.CountAsync(ct);

        // Responses, not day-answers: one person answering counts once however many days they
        // marked. This is the number the poll list sums in its response counts (FR-029).
        var responseCount = await db.Responses
            .CountAsync(r => r.Poll!.RetentionDeadline > clock.Now, ct);

        // !Any() rather than Count() == 0, so SQLite stops at the first row per poll instead of
        // counting every one of them.
        var unanswered = await live.CountAsync(p => !p.Responses.Any(), ct);

        var dueSoon = await live.CountAsync(p => p.RetentionDeadline <= windowEnd, ct);

        // The earliest deadline of *any* live poll, not only of those inside the window, so a
        // dashboard with nothing due this week can still answer "when is the next one?" without a
        // second query. Null exactly when no poll exists - which is a different state from
        // "nothing is due soon", and the view must not collapse the two.
        var nextDeletion = pollCount == 0
            ? null
            : await live.MinAsync(p => (DateTime?)p.RetentionDeadline, ct);

        var distribution = await DistributionAsync(ct);

        return new DashboardView(
            pollCount,
            responseCount,
            unanswered,
            dueSoon,
            nextDeletion,
            distribution.GetValueOrDefault(Availability.Yes),
            distribution.GetValueOrDefault(Availability.Maybe),
            distribution.GetValueOrDefault(Availability.No));
    }

    /// <summary>
    /// Figure 6: the whole system's yes/maybe/no, counted by the database in one grouped pass.
    /// </summary>
    /// <remarks>
    /// <b>Correct by construction, which is the point.</b> A DayAnswer row exists only for a day
    /// somebody actually answered - there is no fourth Availability, and an unanswered day stores
    /// nothing (002 research R-8). A grouped count therefore *cannot* count a non-answer, so this
    /// equals the sum of every poll's per-day summary from <see cref="ResultsProjection"/>: both
    /// are the same grouped count over the same rows, one filtered to a poll and one not (FR-028a).
    /// <para>
    /// There is deliberately no "exclude the unanswered" filter here. None is needed, and one
    /// added later would be a filter that could be forgotten.
    /// </para>
    /// <para>
    /// A group with no rows is absent from the result rather than present as zero, which is why
    /// the caller reads it through GetValueOrDefault. The view then decides whether three zeros
    /// mean "nobody has answered at all" or a real zero among many answers.
    /// </para>
    /// </remarks>
    private async Task<Dictionary<Availability, int>> DistributionAsync(CancellationToken ct)
    {
        var now = clock.Now;

        var grouped = await db.DayAnswers
            .Where(a => a.CandidateDay!.Poll!.RetentionDeadline > now)
            .GroupBy(a => a.Availability)
            .Select(g => new { Availability = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return grouped.ToDictionary(g => g.Availability, g => g.Count);
    }
}
