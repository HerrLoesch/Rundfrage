namespace Rundfrage.Api.Data.Entities;

/// <summary>One participant's claim on one item: a name, and the moment it arrived.</summary>
/// <remarks>
/// Carries no identity, no contact detail and no network metadata, for the same reason
/// <see cref="PollResponse"/> carries none: FR-042 forbids persisting them, and Principle I forbids
/// the duplicate prevention such a column would be for. The rate limiter's partition key lives in
/// memory and is written nowhere (008 FR-023).
/// <para>
/// <see cref="ClaimToken"/> is shared by every claim one submission created, and is therefore
/// <b>not</b> unique. That sharing is the whole mechanism of the personal link: "the entries this
/// link covers" is a query on this column, which is why there is no submission table
/// (008 FR-022b, research R-2).
/// </para>
/// </remarks>
public sealed class WishClaim
{
    public const int DisplayNameMaxLength = 100;

    public Guid Id { get; set; }

    public Guid WishItemId { get; set; }

    public WishItem? WishItem { get; set; }

    /// <summary>A label, never an identity (FR-015). Duplicates are expected and correct (FR-020).</summary>
    public required string DisplayName { get; set; }

    /// <summary>The capability to view and withdraw this submission's claims, and nothing else.</summary>
    public required string ClaimToken { get; set; }

    public DateTime SubmittedAt { get; set; }
}
