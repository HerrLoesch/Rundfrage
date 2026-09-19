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

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Computed once at creation from the last candidate day (FR-039a) so the value shown to the
    /// creator is the value that applies.
    /// </summary>
    /// <remarks>
    /// There is deliberately no status column beside it. Whether a poll is expired is derived by
    /// comparing this to the current instant on every access (FR-039b); a stored flag would be
    /// wrong for as long as its writer lagged behind the deadline.
    /// </remarks>
    public DateTime RetentionDeadline { get; set; }

    /// <summary>
    /// Who owns this, or <c>null</c> for the operator (009 FR-011, FR-012, research R-2).
    /// </summary>
    /// <remarks>
    /// <b><c>null</c> is a value with a meaning, and SQL's three-valued logic will bite.</b>
    /// <c>CreatorId != @id</c> excludes the operator's rows rather than including them, so no
    /// handler writes that predicate by hand: every ownership-respecting read goes through
    /// <c>OwnerScope</c> (009 FR-033, data-model section 5).
    /// <para>
    /// Nullable is also what makes 009 FR-013 a no-op rather than a data migration: everything
    /// stored before that feature is already <c>NULL</c>, and <c>NULL</c> already means the
    /// operator. Nothing had to be rewritten on anybody's production volume.
    /// </para>
    /// <para>
    /// Written once at creation and never updated - not by an edit, not by the operator acting on
    /// somebody else's content (FR-040a), and not by a delete. That last one needs saying because
    /// EF Core's default for an optional relationship is <c>ClientSetNull</c>, which would hand a
    /// deleted Ersteller's content to the operator; <c>RundfrageDbContext</c> states
    /// <c>Cascade</c> explicitly to stop it (FR-020c, research R-4).
    /// </para>
    /// </remarks>
    public Guid? CreatorId { get; set; }

    public Creator? Creator { get; set; }

    public List<CandidateDay> Days { get; set; } = [];

    public List<PollResponse> Responses { get; set; } = [];
}
