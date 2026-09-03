namespace Rundfrage.Api.Data.Entities;

/// <summary>One participant's answers to one poll.</summary>
/// <remarks>
/// Carries no identity, no contact detail, and no network metadata. There is deliberately no
/// column for an IP address or user agent: FR-042 forbids persisting them, and Principle I
/// forbids the duplicate prevention such a column would be for.
/// </remarks>
public sealed class PollResponse
{
    public const int DisplayNameMaxLength = 100;

    public Guid Id { get; set; }

    public Guid PollId { get; set; }

    public Poll? Poll { get; set; }

    /// <summary>A label, never an identity (FR-022). Duplicates are expected and correct.</summary>
    public required string DisplayName { get; set; }

    /// <summary>The capability to revise this response and no other (FR-026, FR-029).</summary>
    public required string EditToken { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }

    public List<DayAnswer> Answers { get; set; } = [];
}
