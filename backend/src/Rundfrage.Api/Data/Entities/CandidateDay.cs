namespace Rundfrage.Api.Data.Entities;

/// <summary>One whole calendar day offered within a poll (FR-011).</summary>
public sealed class CandidateDay
{
    public Guid Id { get; set; }

    public Guid PollId { get; set; }

    public Poll? Poll { get; set; }

    /// <summary>
    /// Stored as a date, not an instant. A candidate day is a label on a calendar; it is
    /// interpreted against Europe/Berlin only where it meets a clock (FR-011a), which is why a
    /// summer-time change cannot shift a stored day.
    /// </summary>
    public DateOnly Date { get; set; }

    public List<DayAnswer> Answers { get; set; } = [];
}
