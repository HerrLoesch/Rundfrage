namespace Rundfrage.Api.Data.Entities;

/// <summary>
/// A Formular: an operator-built survey of arbitrary, typed fields, shared as one participant
/// link (010 FR-001 to FR-011a).
/// </summary>
/// <remarks>
/// <b>There is no owner column here, unlike <see cref="Poll"/> and <see cref="WishList"/>.</b>
/// This feature is deliberately operator-only (010 FR-032); there is no second caller for a
/// nullable <c>CreatorId</c>, so adding one now would be the anticipatory column Principle III
/// rejects (010 research.md R-3).
/// <para>
/// <b>There is equally deliberately no draft/published status column.</b> Whether the link is
/// reachable is derived from whether <see cref="Fields"/> is non-empty (010 FR-011), the same
/// reasoning <see cref="WishList.TargetDate"/> records for "closed": a stored flag would be wrong
/// for as long as its writer lagged behind the field count.
/// </para>
/// <para>
/// There is no retention deadline and no expiry anywhere near it. A form persists until the
/// operator deletes it explicitly (010 FR-043), the same choice feature 008 made for wish lists.
/// </para>
/// </remarks>
public sealed class Form
{
    public const int TitleMaxLength = 200;

    /// <summary>010 FR-011a: adding a 51st field is refused, naming this limit.</summary>
    public const int MaxFields = 50;

    public Guid Id { get; set; }

    public required string Title { get; set; }

    /// <summary>The participant capability (010 FR-012). Unique and indexed - the lookup key.</summary>
    public required string FormToken { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<FormField> Fields { get; set; } = [];

    public List<FormResponse> Responses { get; set; } = [];
}
