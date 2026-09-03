using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;

namespace Rundfrage.Api.Polls;

/// <summary>Matches the schemas of the same names in contracts/openapi.yaml.</summary>
public sealed record DayView(Guid Id, DateOnly Date);

public sealed record DayTotalsView(Guid DayId, int Yes, int Maybe, int No);

public sealed record AnswerView(Guid DayId, string Availability);

public sealed record ResponseRowView(Guid Id, string DisplayName, IReadOnlyList<AnswerView> Answers);

public sealed record PollView(
    string Title,
    string? Message,
    IReadOnlyList<DayView> Days,
    IReadOnlyList<DayTotalsView> Totals,
    IReadOnlyList<ResponseRowView> Responses,
    int Page,
    int PageCount,
    int ResponseCount);

/// <summary>
/// Builds the grid. The totals are computed by the database and the response rows are paged,
/// because FR-036c has to hold at 1000 responses across 100 days - 100,000 cells (research.md R-7).
/// </summary>
public sealed class ResultsProjection(RundfrageDbContext db)
{
    public const int PageSize = 50;

    public static string ToToken(Availability availability) => availability switch
    {
        Availability.Yes => "yes",
        Availability.Maybe => "maybe",
        Availability.No => "no",
        _ => throw new ArgumentOutOfRangeException(nameof(availability), availability, null),
    };

    public async Task<PollView> BuildAsync(Poll poll, int page, CancellationToken ct)
    {
        var days = await db.CandidateDays
            .Where(d => d.PollId == poll.Id)
            .OrderBy(d => d.Date)
            .Select(d => new DayView(d.Id, d.Date))
            .ToListAsync(ct);

        // Counted in SQL. Note there is no fourth bucket: an unanswered day has no row, so it
        // cannot be counted and no filter is needed to exclude it (FR-033, research.md R-8).
        var counted = await db.DayAnswers
            .Where(a => a.CandidateDay!.PollId == poll.Id)
            .GroupBy(a => new { a.CandidateDayId, a.Availability })
            .Select(g => new { g.Key.CandidateDayId, g.Key.Availability, Count = g.Count() })
            .ToListAsync(ct);

        var totals = days
            .Select(d => new DayTotalsView(
                d.Id,
                counted.FirstOrDefault(c => c.CandidateDayId == d.Id && c.Availability == Availability.Yes)?.Count ?? 0,
                counted.FirstOrDefault(c => c.CandidateDayId == d.Id && c.Availability == Availability.Maybe)?.Count ?? 0,
                counted.FirstOrDefault(c => c.CandidateDayId == d.Id && c.Availability == Availability.No)?.Count ?? 0))
            .ToList();

        var responseCount = await db.Responses.CountAsync(r => r.PollId == poll.Id, ct);
        var pageCount = Math.Max(1, (int)Math.Ceiling(responseCount / (double)PageSize));
        var current = Math.Clamp(page, 1, pageCount);

        // Projected with the raw enum and mapped to its wire token afterwards: ToToken is a C#
        // switch, and EF Core cannot translate it into SQL - it would compile happily and throw
        // on the first request.
        var raw = await db.Responses
            .Where(r => r.PollId == poll.Id)
            .OrderBy(r => r.SubmittedAt).ThenBy(r => r.Id)
            .Skip((current - 1) * PageSize)
            .Take(PageSize)
            // No token is projected: a row must never expose anyone's revision capability, not
            // even to the operator (FR-029).
            .Select(r => new
            {
                r.Id,
                r.DisplayName,
                Answers = r.Answers.Select(a => new { a.CandidateDayId, a.Availability }).ToList(),
            })
            .ToListAsync(ct);

        var rows = raw
            .Select(r => new ResponseRowView(
                r.Id,
                r.DisplayName,
                r.Answers.Select(a => new AnswerView(a.CandidateDayId, ToToken(a.Availability))).ToList()))
            .ToList();

        return new PollView(
            poll.Title, poll.Message, days, totals, rows, current, pageCount, responseCount);
    }
}
