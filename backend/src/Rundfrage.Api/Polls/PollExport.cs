using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Time;

namespace Rundfrage.Api.Polls;

/// <summary>Matches PollExport and its schemas in contracts/openapi.yaml.</summary>
public sealed record PollExportDocument(
    int FormatVersion,
    DateTime ExportedAt,
    ExportedPoll Poll,
    IReadOnlyList<ExportedResponse> Responses);

/// <summary>
/// Carries no participant token. The link is a capability, not a property of the poll worth
/// writing into a file that gets passed around (FR-015).
/// </summary>
public sealed record ExportedPoll(
    string Title,
    string? Message,
    IReadOnlyList<ExportedDay> Days);

public sealed record ExportedDay(DateOnly Date);

/// <summary>
/// Carries no edit token. That token is the capability to change this answer, and a downloadable
/// file that contained it would hand that capability to whoever receives the file (FR-015).
/// </summary>
public sealed record ExportedResponse(
    string DisplayName,
    IReadOnlyList<ExportedAnswer> Answers);

/// <summary>
/// Addressed by date rather than by an internal identifier: an export outlives the system that
/// produced it, and a file full of opaque ids would be unreadable on its own (FR-020a).
/// </summary>
public sealed record ExportedAnswer(DateOnly Date, string Availability);

/// <summary>
/// Builds one poll as a JSON document (FR-013 to FR-021a).
/// </summary>
/// <remarks>
/// Separate from <see cref="ResultsProjection"/> although both read a poll. The projection serves
/// a live grid and may change with the interface; the export carries a version and a promise
/// about its shape (FR-020a to FR-020c). Merging them would tie a versioned document to a view.
/// </remarks>
public sealed class PollExport(RundfrageDbContext db, BerlinClock clock)
{
    /// <summary>
    /// Additive changes keep this number; removing a field, renaming one, or changing what an
    /// existing one means raises it (FR-020b). It is a signal, not a promise: nothing commits to
    /// reading an older version back (FR-020c), and there is no import.
    /// </summary>
    public const int FormatVersion = 1;

    public async Task<PollExportDocument> BuildAsync(Poll poll, CancellationToken ct)
    {
        // One transaction for the whole document (FR-019). Assembled from separate reads, an
        // export taken mid-write could catch a response before its answers and describe someone
        // who answered nothing - which in this format is indistinguishable from someone who
        // chose not to answer.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var days = await db.CandidateDays
            .Where(d => d.PollId == poll.Id)
            .OrderBy(d => d.Date)
            .Select(d => new { d.Id, d.Date })
            .ToListAsync(ct);

        // Projected with the raw enum and mapped afterwards: ToToken is a C# switch that EF Core
        // cannot translate, and it would compile happily and throw on the first request.
        var raw = await db.Responses
            .Where(r => r.PollId == poll.Id)
            .OrderBy(r => r.SubmittedAt).ThenBy(r => r.Id)
            .Select(r => new
            {
                r.DisplayName,
                Answers = r.Answers.Select(a => new { a.CandidateDayId, a.Availability }).ToList(),
            })
            .ToListAsync(ct);

        await transaction.CommitAsync(ct);

        var dateOf = days.ToDictionary(d => d.Id, d => d.Date);

        var responses = raw
            .Select(r => new ExportedResponse(
                r.DisplayName,
                r.Answers
                    // A day a participant did not answer has no row, so it is simply absent -
                    // the same meaning absence has in storage (research.md R-8). No placeholder
                    // is invented, because there is nothing to describe.
                    .Where(a => dateOf.ContainsKey(a.CandidateDayId))
                    .Select(a => new ExportedAnswer(
                        dateOf[a.CandidateDayId], ResultsProjection.ToToken(a.Availability)))
                    .OrderBy(a => a.Date)
                    .ToList()))
            .ToList();

        return new PollExportDocument(
            FormatVersion,
            clock.Now,
            new ExportedPoll(poll.Title, poll.Message, days.Select(d => new ExportedDay(d.Date)).ToList()),
            responses);
    }

    /// <summary>
    /// Names the poll and the moment, so several exports can share a folder without overwriting
    /// each other (FR-021a).
    /// </summary>
    public static string FileNameFor(string title, DateTime takenAtUtc)
    {
        var slug = new string(title.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray())
            .Trim('-');

        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }

        // A title can be 300 characters and can be made entirely of punctuation; neither should
        // produce an unusable file name.
        slug = slug.Length > 60 ? slug[..60].Trim('-') : slug;
        slug = slug.Length == 0 ? "umfrage" : slug;

        return $"{slug}-{takenAtUtc:yyyy-MM-dd'T'HHmmss'Z'}.json";
    }
}
