using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Security;
using Rundfrage.Api.Time;

namespace Rundfrage.Api.Polls;

/// <summary>One submitted or revised answer set, as it arrives from the client.</summary>
public sealed record SubmittedAnswer(Guid DayId, string Availability);

public sealed record SubmissionResult(PollResponse? Response, PollError? Error);

/// <summary>Submitting, revising and deleting responses (FR-022 to FR-031, FR-037a).</summary>
public sealed class ResponseService(
    RundfrageDbContext db,
    BerlinClock clock,
    ILogger<ResponseService> logger)
{
    public static PollError? ValidateName(string? displayName) => displayName switch
    {
        null or "" => new PollError("display_name_required"),
        _ when string.IsNullOrWhiteSpace(displayName) => new PollError("display_name_required"),
        { Length: > PollResponse.DisplayNameMaxLength } =>
            new PollError("display_name_too_long", PollResponse.DisplayNameMaxLength),
        _ => null,
    };

    public static bool TryParseAvailability(string? token, out Availability availability)
    {
        availability = token switch
        {
            "yes" => Availability.Yes,
            "maybe" => Availability.Maybe,
            "no" => Availability.No,
            _ => default,
        };

        return availability != default;
    }

    public async Task<SubmissionResult> SubmitAsync(
        Poll poll, string? displayName, IReadOnlyList<SubmittedAnswer> answers, CancellationToken ct)
    {
        var nameError = ValidateName(displayName);
        if (nameError is not null)
        {
            return new SubmissionResult(null, nameError);
        }

        var (mapped, answerError) = await MapAnswersAsync(poll.Id, answers, ct);
        if (answerError is not null)
        {
            return new SubmissionResult(null, answerError);
        }

        // FR-015a under concurrency. The poll row is locked first, so two simultaneous
        // submissions cannot both read 999 and both insert (research.md R-9).
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Polls\" WHERE \"Id\" = {poll.Id} FOR UPDATE", ct);

        var existing = await db.Responses.CountAsync(r => r.PollId == poll.Id, ct);
        if (existing >= Poll.MaxResponses)
        {
            await transaction.RollbackAsync(ct);
            return new SubmissionResult(null, new PollError("poll_full", Poll.MaxResponses));
        }

        var response = new PollResponse
        {
            Id = Guid.CreateVersion7(),
            PollId = poll.Id,
            DisplayName = displayName!.Trim(),
            EditToken = CapabilityToken.Mint(),
            SubmittedAt = clock.Now,
            Answers = mapped,
        };

        db.Responses.Add(response);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        // FR-043a/b: identifiers only. Never the name, the answers or the token.
        logger.LogInformation(
            "Response submitted {ResponseId} to poll {PollId} covering {AnsweredDays} days",
            response.Id, poll.Id, mapped.Count);

        return new SubmissionResult(response, null);
    }

    /// <summary>
    /// FR-030: updates in place and never creates a second response. Omitting a day clears any
    /// answer for it, because absence is the *no answer* state (research.md R-8).
    /// </summary>
    public async Task<SubmissionResult> ReviseAsync(
        PollResponse response, string? displayName, IReadOnlyList<SubmittedAnswer> answers, CancellationToken ct)
    {
        var nameError = ValidateName(displayName);
        if (nameError is not null)
        {
            return new SubmissionResult(null, nameError);
        }

        var (mapped, answerError) = await MapAnswersAsync(response.PollId, answers, ct);
        if (answerError is not null)
        {
            return new SubmissionResult(null, answerError);
        }

        response.DisplayName = displayName!.Trim();

        db.DayAnswers.RemoveRange(
            await db.DayAnswers.Where(a => a.ResponseId == response.Id).ToListAsync(ct));

        foreach (var answer in mapped)
        {
            answer.ResponseId = response.Id;
            db.DayAnswers.Add(answer);
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Response revised {ResponseId} covering {AnsweredDays} days", response.Id, mapped.Count);

        return new SubmissionResult(response, null);
    }

    /// <summary>FR-037a: removes one response without touching the poll or any other.</summary>
    public async Task<bool> DeleteAsync(Guid pollId, Guid responseId, CancellationToken ct)
    {
        var removed = await db.Responses
            .Where(r => r.PollId == pollId && r.Id == responseId)
            .ExecuteDeleteAsync(ct);

        if (removed > 0)
        {
            logger.LogInformation("Response deleted {ResponseId} from poll {PollId}", responseId, pollId);
        }

        return removed > 0;
    }

    private async Task<(List<DayAnswer> Mapped, PollError? Error)> MapAnswersAsync(
        Guid pollId, IReadOnlyList<SubmittedAnswer> answers, CancellationToken ct)
    {
        var validDayIds = await db.CandidateDays
            .Where(d => d.PollId == pollId)
            .Select(d => d.Id)
            .ToListAsync(ct);

        var mapped = new List<DayAnswer>();

        foreach (var answer in answers)
        {
            if (!validDayIds.Contains(answer.DayId))
            {
                // A day from another poll would silently attach an answer where it does not
                // belong, so it is refused rather than skipped.
                return ([], new PollError("unknown_day"));
            }

            if (!TryParseAvailability(answer.Availability, out var availability))
            {
                return ([], new PollError("unknown_day"));
            }

            mapped.Add(new DayAnswer { CandidateDayId = answer.DayId, Availability = availability });
        }

        return (mapped, null);
    }
}
