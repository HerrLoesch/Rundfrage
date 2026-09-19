using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Security;
using Rundfrage.Api.Time;

namespace Rundfrage.Api.Polls;

/// <summary>A validation failure carrying the machine-readable code from the contract.</summary>
public sealed record PollError(string Code, int? Limit = null);

/// <summary>One row of the admin listing, with both counts computed by the database.</summary>
/// <summary>
/// One row of the admin listing, with both counts computed by the database.
/// </summary>
/// <remarks>
/// <paramref name="CreatorId"/> and <paramref name="CreatorName"/> are null together, and that
/// pair means the operator owns it (009 FR-039, research R-2). Null rather than an empty string,
/// so the interface can render it as "Eigene" rather than as a blank cell - a blank cell reads as
/// missing data rather than as "mine".
/// </remarks>
public sealed record PollListItem(
    Guid Id,
    string Title,
    string ParticipantToken,
    DateTime RetentionDeadline,
    int ResponseCount,
    int DayCount,
    Guid? CreatorId = null,
    string? CreatorName = null);

/// <summary>Creating and listing polls (FR-008 to FR-018).</summary>
public sealed class PollService(RundfrageDbContext db, BerlinClock clock, ILogger<PollService> logger)
{
    /// <summary>
    /// Pure so the FR-015 limits are testable without a database, and so there is exactly one
    /// place where "enforced on the server" is true (SC-017).
    /// </summary>
    public static PollError? Validate(string? title, string? message, IReadOnlyCollection<DateOnly> days)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return new PollError("title_required");
        }

        if (title.Length > Poll.TitleMaxLength)
        {
            return new PollError("title_too_long", Poll.TitleMaxLength);
        }

        if (message is { Length: > Poll.MessageMaxLength })
        {
            return new PollError("message_too_long", Poll.MessageMaxLength);
        }

        var distinct = NormaliseDays(days);

        if (distinct.Count == 0)
        {
            return new PollError("days_required");
        }

        // Duplicates count once: selecting the same day twice is one day (FR-012), so it must
        // not consume two of the hundred.
        if (distinct.Count > Poll.MaxCandidateDays)
        {
            return new PollError("too_many_days", Poll.MaxCandidateDays);
        }

        return null;
    }

    /// <summary>FR-012 and FR-013: stored once, chronological regardless of selection order.</summary>
    public static IReadOnlyList<DateOnly> NormaliseDays(IEnumerable<DateOnly> days) =>
        days.Distinct().Order().ToArray();

    /// <summary>
    /// Creates a poll owned by <paramref name="owner"/>, or by the operator when that is null
    /// (009 FR-011, FR-012, FR-022).
    /// </summary>
    /// <remarks>
    /// The owner is the whole of what feature 009 adds here. Everything else - the limits, the
    /// day normalisation, the token, the retention deadline - is unchanged, which is why an
    /// Ersteller's poll is an ordinary poll in every other respect (009 FR-014).
    /// <para>
    /// Written once and never updated. There is no method to change it, deliberately: ownership
    /// is fixed at creation and never transfers (009 FR-012, FR-040a).
    /// </para>
    /// </remarks>
    public async Task<Poll> CreateAsync(
        string title, string? message, IReadOnlyCollection<DateOnly> days, CancellationToken ct,
        Guid? owner = null)
    {
        var normalised = NormaliseDays(days);

        var poll = new Poll
        {
            Id = Guid.CreateVersion7(),
            Title = title.Trim(),
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            ParticipantToken = CapabilityToken.Mint(),
            CreatedAt = clock.Now,
            // Derived once, so the deadline the creator was shown is the one that applies (FR-039a).
            RetentionDeadline = clock.RetentionDeadlineFor(normalised[^1]),
            Days = [.. normalised.Select(d => new CandidateDay { Id = Guid.CreateVersion7(), Date = d })],
            CreatorId = owner,
        };

        db.Polls.Add(poll);
        await db.SaveChangesAsync(ct);

        // FR-043a. The identifier only - never the title, the token, or anything a participant wrote.
        logger.LogInformation("Poll created {PollId} with {DayCount} days", poll.Id, poll.Days.Count);

        return poll;
    }

    /// <summary>
    /// FR-018. Expired polls are filtered out here too: they are unreachable from the moment the
    /// deadline passes, not from the moment the sweep runs (FR-039b).
    /// </summary>
    /// <remarks>
    /// Projects the two counts in SQL rather than loading the rows.
    /// <para>
    /// This previously returned entities with only <c>Days</c> loaded, while the response count
    /// was read from the un-loaded <c>Responses</c> collection - which is empty, so every poll
    /// reported zero answers no matter how many it held. It failed silently, and FR-038 makes
    /// that count part of the deletion confirmation: the operator would have been told "0
    /// responses will be destroyed" while destroying all of them.
    /// </para>
    /// <para>
    /// Counting in the database also avoids fetching up to 1000 rows per poll merely to call
    /// <c>Count</c> on them.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Removes one poll and, by cascade, its days and responses (002 FR-037).
    /// </summary>
    /// <remarks>
    /// Added by feature 009 so that a creator handler can delete a poll without being handed
    /// <c>RundfrageDbContext</c> - the admin endpoint deletes inline, which is fine there and would
    /// be a hole here, because an unscoped queryable in reach of a creator handler is exactly what
    /// 009 FR-033 exists to prevent (009 research R-1).
    /// <para>
    /// Takes an id rather than an entity, and the caller is responsible for having established
    /// that it may touch it. Both callers do: the admin endpoint through the retention filter, the
    /// creator endpoint through <c>OwnerScope</c>.
    /// </para>
    /// </remarks>
    public async Task<bool> DeleteAsync(Guid pollId, CancellationToken ct)
    {
        var removed = await db.Polls.Where(p => p.Id == pollId).ExecuteDeleteAsync(ct);

        if (removed > 0)
        {
            logger.LogInformation("Poll deleted {PollId}", pollId);
        }

        return removed > 0;
    }

    public async Task<List<PollListItem>> ListAsync(CancellationToken ct) =>
        await db.Polls
            .Where(p => p.RetentionDeadline > clock.Now)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PollListItem(
                p.Id,
                p.Title,
                p.ParticipantToken,
                p.RetentionDeadline,
                p.Responses.Count,
                p.Days.Count,
                p.CreatorId,
                // Null for the operator's own. Read through the navigation rather than joined by
                // hand, so the null case needs no special handling (009 FR-039).
                p.Creator == null ? null : p.Creator.Name))
            .ToListAsync(ct);
}
