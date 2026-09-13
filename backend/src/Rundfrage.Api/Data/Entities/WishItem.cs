namespace Rundfrage.Api.Data.Entities;

/// <summary>
/// One thing wished for within a list. Its <see cref="WantedCount"/> is the number of claims it
/// can hold - capacity, not a suggestion (008 FR-017).
/// </summary>
public sealed class WishItem
{
    public const int NameMaxLength = 200;
    public const int MaxWantedCount = 50;

    /// <summary>FR-006: an item the operator gave no number to is wanted exactly once.</summary>
    public const int DefaultWantedCount = 1;

    public Guid Id { get; set; }

    public Guid WishListId { get; set; }

    public WishList? WishList { get; set; }

    /// <summary>Unique within its list (FR-008), enforced by an index rather than by goodwill.</summary>
    public required string Name { get; set; }

    public int WantedCount { get; set; } = DefaultWantedCount;

    /// <summary>
    /// The order the operator entered the items in, and the order everybody sees them in (FR-009).
    /// </summary>
    public int Position { get; set; }

    public List<WishClaim> Claims { get; set; } = [];
}
