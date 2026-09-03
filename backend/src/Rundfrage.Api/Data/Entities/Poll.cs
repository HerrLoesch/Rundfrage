namespace Rundfrage.Api.Data.Entities;

/// <summary>A date poll. Owns its days and its responses; deleting it destroys both.</summary>
public sealed class Poll
{
    public const int TitleMaxLength = 300;
    public const int MessageMaxLength = 2000;
    public const int MaxCandidateDays = 100;
    public const int MaxResponses = 1000;

    public Guid Id { get; set; }

    public required string Title { get; set; }

    public string? Message { get; set; }

    /// <summary>The participant capability (FR-016). Unique and indexed - it is the lookup key.</summary>
    public required string ParticipantToken { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Computed once at creation from the last candidate day (FR-039a) so the value shown to the
    /// creator is the value that applies.
    /// </summary>
    /// <remarks>
    /// There is deliberately no status column beside it. Whether a poll is expired is derived by
    /// comparing this to the current instant on every access (FR-039b); a stored flag would be
    /// wrong for as long as its writer lagged behind the deadline.
    /// </remarks>
    public DateTimeOffset RetentionDeadline { get; set; }

    public List<CandidateDay> Days { get; set; } = [];

    public List<PollResponse> Responses { get; set; } = [];
}
