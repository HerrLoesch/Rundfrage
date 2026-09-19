using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;

namespace Rundfrage.Api.Creators;

/// <summary>One row of the Ersteller area (009 FR-044, FR-044a).</summary>
/// <remarks>
/// <b><see cref="HasLink"/> is derived, not stored.</b> It is the token being present, which is
/// what "revoked" means (research R-3). There is no status column for it to disagree with.
/// <para>
/// <see cref="LinkToken"/> is present exactly when <see cref="HasLink"/> is true. The operator
/// re-copies the link from here rather than keeping their own note of it, which is the whole point
/// of FR-044 - a link the operator cannot retrieve is a link they cannot re-send.
/// </para>
/// <para>
/// No participant display name appears here and none may be added. This record reaches the
/// dashboard through the areas that render it, and 008 FR-050 forbids a display name there.
/// </para>
/// </remarks>
public sealed record CreatorSummary(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    bool HasLink,
    string? LinkToken,
    int PollCount,
    int WishListCount);

/// <summary>
/// Every Ersteller with its counts, in a fixed number of queries.
/// </summary>
/// <remarks>
/// <b>Fixed, not proportional.</b> One query, with both counts computed by the database - the same
/// decision <c>WishListProjection</c> records, and for the same reason: a count per Ersteller would
/// be a hundred round trips inside one request at the limit of FR-009.
/// <para>
/// A revoked Ersteller is listed like any other, with its counts and without a link (FR-044a). It
/// is not hidden and not sorted to the bottom: the operator's next decision - new link, or delete -
/// needs exactly those figures in front of them.
/// </para>
/// </remarks>
public sealed class CreatorProjection(RundfrageDbContext db)
{
    public async Task<IReadOnlyList<CreatorSummary>> ListAsync(CancellationToken ct) =>
        await db.Creators
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CreatorSummary(
                c.Id,
                c.Name,
                c.CreatedAt,
                c.LinkToken != null,
                c.LinkToken,
                c.Polls.Count,
                c.WishLists.Count))
            .ToListAsync(ct);

    /// <summary>One Ersteller, for the response to a create, rename or reissue.</summary>
    public async Task<CreatorSummary?> FindAsync(Guid id, CancellationToken ct) =>
        await db.Creators
            .Where(c => c.Id == id)
            .Select(c => new CreatorSummary(
                c.Id,
                c.Name,
                c.CreatedAt,
                c.LinkToken != null,
                c.LinkToken,
                c.Polls.Count,
                c.WishLists.Count))
            .FirstOrDefaultAsync(ct);
}
