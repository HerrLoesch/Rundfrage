namespace Rundfrage.Api.Data.Entities;

/// <summary>
/// A wish list: an occasion something is needed for. Owns its items; deleting it destroys the
/// items and their claims (008 FR-039).
/// </summary>
/// <remarks>
/// There is deliberately no retention deadline here and no expiry anywhere near it. A poll erases
/// itself thirty days after its last candidate day; a wish list is removed only when the operator
/// says so (FR-037), and that explicit deletion path is its retention outcome under Principle IV.
/// <para>
/// There is equally deliberately no <c>ClosedAt</c> or status column. Whether the list is closed is
/// derived by comparing <see cref="TargetDate"/> to the clock on every access (FR-028c) - the same
/// reasoning <see cref="Poll.RetentionDeadline"/> records for expiry. A stored flag would be wrong
/// for as long as its writer lagged behind the day boundary, and moving the target date would then
/// need a second writer to repair it.
/// </para>
/// </remarks>
public sealed class WishList
{
    public const int TitleMaxLength = 300;
    public const int DescriptionMaxLength = 2000;
    public const int MaxItems = 100;

    /// <summary>
    /// The wanted counts of one list's items, added together, may not exceed this (FR-010).
    /// </summary>
    /// <remarks>
    /// Deliberately a cap on <i>places</i> rather than a separate cap on claims. The wanted counts
    /// already bound how many claims a list can hold, so a second independent ceiling could only
    /// contradict FR-017: a list could show open places that nothing would accept.
    /// </remarks>
    public const int MaxPlaces = 1000;

    public Guid Id { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    /// <summary>The day the things are needed for (FR-002). Past days are permitted (FR-002a).</summary>
    public DateOnly TargetDate { get; set; }

    /// <summary>The participant capability (FR-012). Unique and indexed - it is the lookup key.</summary>
    public required string ListToken { get; set; }

    public DateTime CreatedAt { get; set; }

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

    public List<WishItem> Items { get; set; } = [];
}
