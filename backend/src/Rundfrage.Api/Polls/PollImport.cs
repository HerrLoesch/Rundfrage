using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Http;
using Rundfrage.Api.Security;
using Rundfrage.Api.Time;

namespace Rundfrage.Api.Polls;

/// <summary>One thing the file held that was not taken, and why (FR-003, FR-012).</summary>
public sealed record ImportSkip(string Kind, string Reason, string? Detail = null);

/// <summary>What arrived.</summary>
public sealed record ImportCounts(int Days, int Responses, int Answers);

/// <summary>
/// The answer to one import request. Not stored, and there is no history to return to (FR-003a).
/// </summary>
public sealed record ImportSummary(
    bool Imported,
    Guid? PollId,
    string? ParticipantToken,
    ImportCounts Counts,
    IReadOnlyList<ImportSkip> Skipped);

/// <summary>A file that could not yield a poll at all. Nothing was created.</summary>
public sealed record ImportRefusal(string Code, int? Limit = null);

/// <summary>
/// Reads a poll export back in as a new poll (FR-007 to FR-015).
/// </summary>
/// <remarks>
/// <b>Refusal and skipping are different outcomes, and the whole class is organised around the
/// difference.</b> A refusal means the document cannot yield a poll — nothing is created and one
/// code says why. A skip means the document is sound but one thing in it cannot be taken; that
/// thing is left out, named in the summary, and everything else still arrives (FR-012). Silently
/// dropping either would be the one failure this feature exists to prevent.
/// <para>
/// The response limit is deliberately a refusal rather than a truncation: skipping the 1001st
/// response would mean choosing which answers to discard, and no ordering in the file makes that
/// choice defensible.
/// </para>
/// <para>
/// Nothing here reads a token, because the format carries none (003 FR-015). The imported poll and
/// each of its responses get fresh ones, so every link previously shared for the original is dead
/// — which is a consequence of the format, not a decision taken here (FR-011).
/// </para>
/// </remarks>
public sealed class PollImport(RundfrageDbContext db, BerlinClock clock, ILogger<PollImport> logger)
{
    /// <summary>The one version this reads. A higher one is refused rather than interpreted.</summary>
    public const int SupportedFormatVersion = PollExport.FormatVersion;

    public async Task<(ImportRefusal? Refusal, ImportSummary? Summary)> ImportAsync(
        Stream document, CancellationToken ct)
    {
        JsonDocument parsed;

        try
        {
            parsed = await JsonDocument.ParseAsync(document, cancellationToken: ct);
        }
        catch (JsonException)
        {
            // Type only - never the content, and never the exception's message, which quotes it
            // (FR-005).
            return (Refused(ErrorCodes.NotAValidExport), null);
        }

        using (parsed)
        {
            return await ReadAsync(parsed.RootElement, ct);
        }
    }

    private async Task<(ImportRefusal? Refusal, ImportSummary? Summary)> ReadAsync(
        JsonElement root, CancellationToken ct)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return (Refused(ErrorCodes.NotAValidExport), null);
        }

        // --- Version ---------------------------------------------------------------------
        if (!root.TryGetProperty("formatVersion", out var versionElement)
            || versionElement.ValueKind != JsonValueKind.Number
            || !versionElement.TryGetInt32(out var version))
        {
            return (Refused(ErrorCodes.NotAValidExport), null);
        }

        if (version > SupportedFormatVersion)
        {
            return (Refused(ErrorCodes.FormatVersionTooNew, SupportedFormatVersion), null);
        }

        // --- The poll --------------------------------------------------------------------
        if (!root.TryGetProperty("poll", out var pollElement) || pollElement.ValueKind != JsonValueKind.Object)
        {
            return (Refused(ErrorCodes.NotAValidExport), null);
        }

        if (!pollElement.TryGetProperty("title", out var titleElement)
            || titleElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(titleElement.GetString()))
        {
            return (Refused(ErrorCodes.NotAValidExport), null);
        }

        var title = titleElement.GetString()!;
        string? message = null;

        if (pollElement.TryGetProperty("message", out var messageElement)
            && messageElement.ValueKind == JsonValueKind.String)
        {
            message = messageElement.GetString();
        }

        if (!TryReadDays(pollElement, out var days))
        {
            return (Refused(ErrorCodes.NotAValidExport), null);
        }

        // The by-hand creation path's limits, enforced identically (FR-014). Reusing Validate
        // rather than restating the numbers is what keeps "enforced on the server" one place.
        if (PollService.Validate(title, message, days) is { } invalid)
        {
            return (Refused(
                invalid.Code is "title_required" or "days_required"
                    ? ErrorCodes.NotAValidExport
                    : ErrorCodes.PollTooLarge,
                invalid.Limit), null);
        }

        var normalisedDays = PollService.NormaliseDays(days);

        // --- The responses, structurally ---------------------------------------------------
        var responseElements = new List<JsonElement>();

        if (root.TryGetProperty("responses", out var responsesElement))
        {
            if (responsesElement.ValueKind != JsonValueKind.Array)
            {
                return (Refused(ErrorCodes.NotAValidExport), null);
            }

            responseElements.AddRange(responsesElement.EnumerateArray());
        }

        if (responseElements.Count > Poll.MaxResponses)
        {
            return (Refused(ErrorCodes.PollTooLarge, Poll.MaxResponses), null);
        }

        // --- Retention: a poll already past its deadline is skipped, not refused (FR-012) ---
        var deadline = clock.RetentionDeadlineFor(normalisedDays[^1]);
        var skipped = new List<ImportSkip>();

        if (deadline <= clock.Now)
        {
            skipped.Add(new ImportSkip("poll", "already_expired", normalisedDays[^1].ToString("yyyy-MM-dd")));

            logger.LogInformation("Import took nothing: the poll is already past its retention date");

            return (null, new ImportSummary(false, null, null, new ImportCounts(0, 0, 0), skipped));
        }

        return await CreateAsync(title, message, normalisedDays, responseElements, deadline, skipped, ct);
    }

    private static bool TryReadDays(JsonElement poll, out List<DateOnly> days)
    {
        days = [];

        if (!poll.TryGetProperty("days", out var daysElement) || daysElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var entry in daysElement.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object
                || !entry.TryGetProperty("date", out var dateElement)
                || dateElement.ValueKind != JsonValueKind.String
                || !DateOnly.TryParse(dateElement.GetString(), out var date))
            {
                return false;
            }

            days.Add(date);
        }

        return days.Count > 0;
    }

    private async Task<(ImportRefusal?, ImportSummary?)> CreateAsync(
        string title,
        string? message,
        IReadOnlyList<DateOnly> days,
        List<JsonElement> responseElements,
        DateTime deadline,
        List<ImportSkip> skipped,
        CancellationToken ct)
    {
        var now = clock.Now;

        var poll = new Poll
        {
            Id = Guid.CreateVersion7(),
            Title = title.Trim(),
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            ParticipantToken = CapabilityToken.Mint(),
            CreatedAt = now,
            RetentionDeadline = deadline,
            Days = [.. days.Select(d => new CandidateDay { Id = Guid.CreateVersion7(), Date = d })],
        };

        var dayByDate = poll.Days.ToDictionary(d => d.Date, d => d.Id);
        var answerCount = 0;

        foreach (var element in responseElements)
        {
            var response = ReadResponse(element, dayByDate, now, skipped);

            if (response is null)
            {
                continue;
            }

            poll.Responses.Add(response);
            answerCount += response.Answers.Count;
        }

        // FR-013: the poll and everything accepted with it arrive together or not at all. What was
        // skipped was never accepted, so its absence does not break "together".
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        db.Polls.Add(poll);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        // FR-005 and 002 FR-043a: identifiers and counts, never a title, a name or a token.
        logger.LogInformation(
            "Imported poll {PollId} with {DayCount} days, {ResponseCount} responses and {SkipCount} skipped",
            poll.Id, poll.Days.Count, poll.Responses.Count, skipped.Count);

        return (null, new ImportSummary(
            true,
            poll.Id,
            poll.ParticipantToken,
            new ImportCounts(poll.Days.Count, poll.Responses.Count, answerCount),
            skipped));
    }

    private static PollResponse? ReadResponse(
        JsonElement element,
        IReadOnlyDictionary<DateOnly, Guid> dayByDate,
        DateTime now,
        List<ImportSkip> skipped)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("displayName", out var nameElement)
            || nameElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(nameElement.GetString()))
        {
            skipped.Add(new ImportSkip("response", "name_required"));
            return null;
        }

        var name = nameElement.GetString()!;

        if (name.Length > PollResponse.DisplayNameMaxLength)
        {
            skipped.Add(new ImportSkip(
                "response", "name_too_long", PollResponse.DisplayNameMaxLength.ToString()));
            return null;
        }

        var answers = new List<DayAnswer>();
        var seenDays = new HashSet<DateOnly>();
        var pending = new List<ImportSkip>();

        if (element.TryGetProperty("answers", out var answersElement)
            && answersElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var answer in answersElement.EnumerateArray())
            {
                if (answer.ValueKind != JsonValueKind.Object
                    || !answer.TryGetProperty("date", out var dateElement)
                    || dateElement.ValueKind != JsonValueKind.String
                    || !DateOnly.TryParse(dateElement.GetString(), out var date))
                {
                    pending.Add(new ImportSkip("answer", "unknown_day"));
                    continue;
                }

                if (!dayByDate.TryGetValue(date, out var dayId))
                {
                    pending.Add(new ImportSkip("answer", "unknown_day", date.ToString("yyyy-MM-dd")));
                    continue;
                }

                if (!seenDays.Add(date))
                {
                    // Two answers for one day, and nothing in the format says which wins. The
                    // whole response is left out rather than guessed at.
                    skipped.Add(new ImportSkip("response", "duplicate_day", date.ToString("yyyy-MM-dd")));
                    return null;
                }

                if (!TryReadAvailability(answer, out var availability))
                {
                    pending.Add(new ImportSkip(
                        "answer", "unknown_availability", date.ToString("yyyy-MM-dd")));
                    continue;
                }

                answers.Add(new DayAnswer { CandidateDayId = dayId, Availability = availability });
            }
        }

        skipped.AddRange(pending);

        return new PollResponse
        {
            Id = Guid.CreateVersion7(),
            DisplayName = name.Trim(),
            EditToken = CapabilityToken.Mint(),
            // The format records no timestamp, so the import instant is the only honest value and
            // file order is the only order (spec Assumptions).
            SubmittedAt = now,
            Answers = answers,
        };
    }

    private static bool TryReadAvailability(JsonElement answer, out Availability availability)
    {
        availability = default;

        if (!answer.TryGetProperty("availability", out var element)
            || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        switch (element.GetString())
        {
            case "yes": availability = Availability.Yes; return true;
            case "maybe": availability = Availability.Maybe; return true;
            case "no": availability = Availability.No; return true;
            default: return false;
        }
    }

    private ImportRefusal Refused(string code, int? limit = null)
    {
        logger.LogInformation("Import refused ({Code})", code);
        return new ImportRefusal(code, limit);
    }
}
