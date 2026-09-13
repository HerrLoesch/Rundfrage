using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Time;

namespace Rundfrage.Api.Wishes;

/// <summary>One row of the wish-list area's list and of the dashboard's overview.</summary>
/// <remarks>
/// Counts, never a percentage. The filled share is formatted by the interface and rounded down,
/// so 999 of 1000 reads 99 % and only a genuinely complete list reads 100 % (research R-7). A
/// rounded number on the wire could not be checked against a hand count, which SC-004 requires.
/// <para>
/// No participant name appears here. The dashboard renders this payload, and FR-050 forbids a
/// display name on the dashboard - keeping names out of the row makes that structural rather than
/// a rule somebody has to remember while writing the template.
/// </para>
/// </remarks>
public sealed record WishListSummary(
    Guid Id,
    string Title,
    DateOnly TargetDate,
    bool Closed,
    int ItemCount,
    int EntryCount,
    int PlaceCount,
    int UntakenItemCount,
    int CompleteItemCount,
    string ListToken);

/// <summary>
/// The figures of FR-045, for every list, in a fixed number of queries.
/// </summary>
/// <remarks>
/// <b>Fixed, not proportional.</b> Two queries whatever the installation holds: one for the lists,
/// one grouped aggregate that returns a single row per list. Not one query per list, which would be
/// 200 round trips inside one request at the scale FR-054 documents - the same trap 007 FR-028b
/// names for the dashboard - and not a full read of the item table to count it in memory, which
/// costs 20,000 rows on the wire to produce 200 numbers.
/// <para>
/// This projection has two readers - the wish-list area and the dashboard - and that is
/// deliberate: FR-049 requires them to agree, and one projection makes agreement structural
/// rather than something a test has to keep true (research R-5).
/// </para>
/// </remarks>
public sealed class WishListProjection(RundfrageDbContext db, BerlinClock clock)
{
    public async Task<IReadOnlyList<WishListSummary>> ListAsync(CancellationToken ct)
    {
        var lists = await db.WishLists
            .Select(l => new { l.Id, l.Title, l.TargetDate, l.ListToken })
            .ToListAsync(ct);

        if (lists.Count == 0)
        {
            return [];
        }

        // One grouped aggregate, evaluated by the database: every figure FR-045 asks for arrives
        // as one row per list. The alternative that was written first pulled every item row back
        // and grouped them in memory, which is 20,000 rows at the scale FR-054 documents - it met
        // the budget, but it carried the whole table across the wire to count it.
        var figures = await db.WishItems
            .GroupBy(i => i.WishListId)
            .Select(g => new
            {
                WishListId = g.Key,
                ItemCount = g.Count(),
                PlaceCount = g.Sum(i => i.WantedCount),
                EntryCount = g.Sum(i => i.Claims.Count),
                UntakenItemCount = g.Count(i => i.Claims.Count == 0),
                CompleteItemCount = g.Count(i => i.Claims.Count >= i.WantedCount),
            })
            .ToDictionaryAsync(f => f.WishListId, ct);

        var summaries = lists.Select(list =>
        {
            // A list with no items has no row in the aggregate above, and its figures are zeros
            // rather than an absence.
            var own = figures.GetValueOrDefault(list.Id);

            return new WishListSummary(
                list.Id,
                list.Title,
                list.TargetDate,
                // Derived here, stored nowhere (FR-028c).
                clock.DayHasEnded(list.TargetDate),
                own?.ItemCount ?? 0,
                own?.EntryCount ?? 0,
                own?.PlaceCount ?? 0,
                own?.UntakenItemCount ?? 0,
                own?.CompleteItemCount ?? 0,
                list.ListToken);
        }).ToArray();

        // FR-048c: open lists first with the nearest target date on top, then closed lists with
        // the most recently closed first. Ordered here rather than in the interface, so the area
        // and the dashboard cannot disagree about order either.
        return
        [
            .. summaries.Where(s => !s.Closed).OrderBy(s => s.TargetDate).ThenBy(s => s.Title),
            .. summaries.Where(s => s.Closed).OrderByDescending(s => s.TargetDate).ThenBy(s => s.Title),
        ];
    }

    /// <summary>The detail payload: items in the operator's order, with the names on them.</summary>
    public WishListDetail Detail(WishList list) => new(
        list.Id,
        list.Title,
        list.Description,
        list.TargetDate,
        clock.DayHasEnded(list.TargetDate),
        list.ListToken,
        list.Items.Sum(i => i.Claims.Count),
        list.Items.Sum(i => i.WantedCount),
        [.. list.Items.OrderBy(i => i.Position).Select(i => new WishItemDetail(
            i.Id,
            i.Name,
            i.WantedCount,
            i.Position,
            [.. i.Claims.OrderBy(c => c.SubmittedAt)
                .Select(c => new WishClaimView(c.Id, c.DisplayName))]))]);

    /// <summary>What a link holder sees. Names, because FR-019 requires them; nothing else.</summary>
    public ParticipantWishList Participant(WishList list) => new(
        list.Title,
        list.Description,
        list.TargetDate,
        clock.DayHasEnded(list.TargetDate),
        [.. list.Items.OrderBy(i => i.Position).Select(i => new ParticipantWishItem(
            i.Id,
            i.Name,
            i.WantedCount,
            Math.Max(0, i.WantedCount - i.Claims.Count),
            [.. i.Claims.OrderBy(c => c.SubmittedAt).Select(c => c.DisplayName)]))]);
}

public sealed record WishClaimView(Guid Id, string DisplayName);

public sealed record WishItemDetail(
    Guid Id, string Name, int WantedCount, int Position, IReadOnlyList<WishClaimView> Claims);

public sealed record WishListDetail(
    Guid Id,
    string Title,
    string? Description,
    DateOnly TargetDate,
    bool Closed,
    string ListToken,
    int EntryCount,
    int PlaceCount,
    IReadOnlyList<WishItemDetail> Items);

public sealed record ParticipantWishItem(
    Guid Id, string Name, int WantedCount, int OpenPlaces, IReadOnlyList<string> Names);

public sealed record ParticipantWishList(
    string Title,
    string? Description,
    DateOnly TargetDate,
    bool Closed,
    IReadOnlyList<ParticipantWishItem> Items);
