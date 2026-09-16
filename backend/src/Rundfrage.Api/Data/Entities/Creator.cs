namespace Rundfrage.Api.Data.Entities;

/// <summary>
/// An Ersteller: somebody the operator names and hands one link to, who then creates their own
/// date polls and wish lists through it (009 FR-001 to FR-009).
/// </summary>
/// <remarks>
/// <b>There is no account here, and there is deliberately nothing to make one out of.</b> No
/// password, no email address, no telephone number, no last-seen and no usage counter (FR-003,
/// Principle IV). The <see cref="Name"/> is a label the operator writes for the operator - the same
/// kind of string as a poll title - and nobody types it to gain access.
/// <para>
/// <b>There is equally deliberately no <c>RevokedAt</c> and no status column.</b> Whether an
/// Ersteller can currently get in is <see cref="LinkToken"/> being present, which is why that
/// column is nullable (FR-018, FR-020, research R-3). A stored flag beside a live token would be
/// wrong for as long as the two disagreed, and the direction it would be wrong in is the dangerous
/// one: a revoked link that still resolves. The same reasoning <see cref="Poll.RetentionDeadline"/>
/// records for expiry and <see cref="WishList.TargetDate"/> for closing.
/// </para>
/// <para>
/// <b>And there is no audit trail.</b> Nothing records that the operator edited an Ersteller's wish
/// list, and nothing notifies them of it (FR-040b). That is a refusal rather than an omission: a
/// notification would need a contact detail this row does not hold, and an audit trail records more
/// than the feature needs.
/// </para>
/// </remarks>
public sealed class Creator
{
    public const int NameMaxLength = 100;

    /// <summary>
    /// At most this many Ersteller exist at any one time (FR-009). Unlike the documented scales of
    /// 007 FR-028c and 008 FR-054 this is an <i>enforced</i> maximum: the request states it as a
    /// bound on the system, and the operator meets it where they act.
    /// </summary>
    /// <remarks>
    /// A place is freed by <i>deleting</i> an Ersteller, never by revoking one: a revoked Ersteller
    /// still exists and still owns its content, so it still occupies a place (FR-009a).
    /// </remarks>
    public const int MaxCreators = 100;

    public Guid Id { get; set; }

    /// <summary>The operator's label for this person (FR-001). Unique, case-insensitively (FR-002).</summary>
    public required string Name { get; set; }

    /// <summary>
    /// The capability (FR-004). <c>null</c> means revoked - the link stops resolving and the
    /// Ersteller keeps everything it owns (FR-018, FR-020).
    /// </summary>
    /// <remarks>
    /// Unique and indexed, and unique across <c>NULL</c>s too: SQLite treats them as distinct, so
    /// any number of revoked Ersteller coexist. That is a SQLite-specific guarantee this schema
    /// depends on, and <c>SchemaCreationTests</c> asserts it rather than leaving it to folklore.
    /// </remarks>
    public string? LinkToken { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Polls this Ersteller owns. Destroyed with it, never reassigned (FR-020a, FR-020c).</summary>
    public List<Poll> Polls { get; set; } = [];

    /// <summary>Wish lists this Ersteller owns. Destroyed with it, never reassigned.</summary>
    public List<WishList> WishLists { get; set; } = [];
}
