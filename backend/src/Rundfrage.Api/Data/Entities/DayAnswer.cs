namespace Rundfrage.Api.Data.Entities;

/// <summary>What a participant said about one day.</summary>
/// <remarks>
/// <b>Three values, not four.</b> There is no <c>NoAnswer</c>, because an unanswered day stores
/// nothing at all - the absence of a row is the state (FR-024, research.md R-8). That is what
/// makes FR-033 true by construction: a grouped count cannot count what was never written, so
/// the per-day totals need no filter that could later be forgotten.
/// </remarks>
public enum Availability
{
    Yes = 1,
    Maybe = 2,
    No = 3,
}

/// <summary>The intersection of one response and one candidate day.</summary>
public sealed class DayAnswer
{
    public Guid ResponseId { get; set; }

    public PollResponse? Response { get; set; }

    public Guid CandidateDayId { get; set; }

    public CandidateDay? CandidateDay { get; set; }

    public Availability Availability { get; set; }
}
