using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Security;
using Rundfrage.Api.Time;

namespace Rundfrage.Api.Polls;

/// <summary>A validation failure carrying the machine-readable code from the contract.</summary>
public sealed record PollError(string Code, int? Limit = null);

/// <summary>One row of the admin listing, with both counts computed by the database.</summary>
public sealed record PollListItem(
    Guid Id,
    string Title,
    string ParticipantToken,
    DateTimeOffset RetentionDeadline,
    int ResponseCount,
    int DayCount);

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

    public async Task<Poll> CreateAsync(
        string title, string? message, IReadOnlyCollection<DateOnly> days, CancellationToken ct)
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
                p.Days.Count))
            .ToListAsync(ct);
}
