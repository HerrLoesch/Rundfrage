using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Http;
using Rundfrage.Api.Security;
using Rundfrage.Api.Time;

namespace Rundfrage.Api.Wishes;

/// <summary>A refusal carrying the machine-readable code from the contract.</summary>
/// <remarks>
/// <paramref name="Limit"/> is the number that makes the refusal actionable: the character limit,
/// the places still available (008 FR-010a), or the claims already made (FR-033). A refusal that
/// says only "no" is one the operator cannot act on.
/// <para>
/// <paramref name="Detail"/> is the same idea where the actionable part is a word rather than a
/// number - today only the name a duplicate collides with (FR-008, ui-contract section 4). An
/// operator who typed five items and got "that name is taken" has to guess which one; naming it
/// removes the guess. It carries operator-written text only, never a participant's display name.
/// </para>
/// </remarks>
public sealed record WishError(string Code, int? Limit = null, string? Detail = null);

/// <summary>One item as it arrives from the client, before anything has been decided about it.</summary>
public sealed record WishItemDraft(string? Name, int? WantedCount);

/// <summary>Creating, editing and deleting wish lists (008 FR-001 to FR-040).</summary>
public sealed class WishListService(
    RundfrageDbContext db, BerlinClock clock, ILogger<WishListService> logger)
{
    /// <summary>FR-006: an item the operator gave no number to is wanted exactly once.</summary>
    public static int WantedCountOf(WishItemDraft draft) =>
        draft.WantedCount ?? WishItem.DefaultWantedCount;

    /// <summary>
    /// Pure, so the limits of FR-010 are testable without a database and so there is exactly one
    /// place where "enforced on the server, not only in the form" is true.
    /// </summary>
    public static WishError? ValidateList(
        string? title, string? description, DateOnly? targetDate, IReadOnlyList<WishItemDraft> items)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return new WishError(ErrorCodes.TitleRequired);
        }

        if (title.Length > WishList.TitleMaxLength)
        {
            return new WishError(ErrorCodes.TitleTooLong, WishList.TitleMaxLength);
        }

        if (description is { Length: > WishList.DescriptionMaxLength })
        {
            return new WishError(ErrorCodes.DescriptionTooLong, WishList.DescriptionMaxLength);
        }

        // FR-002. A past date is permitted (FR-002a) - this only asks that there is one.
        if (targetDate is null)
        {
            return new WishError(ErrorCodes.TargetDateRequired);
        }

        if (items.Count == 0)
        {
            return new WishError(ErrorCodes.ItemsRequired);
        }

        foreach (var item in items)
        {
            var error = ValidateItem(item);
            if (error is not null)
            {
                return error;
            }
        }

        // FR-008, checked before the counts so that "Kuchen twice" is named as the duplicate it
        // is rather than as an arithmetic problem.
        var names = items.Select(i => Normalise(i.Name!)).ToArray();
        var duplicate = names
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            return new WishError(ErrorCodes.DuplicateItemName, Detail: duplicate.Key);
        }

        if (items.Count > WishList.MaxItems)
        {
            return new WishError(ErrorCodes.TooManyItems, WishList.MaxItems);
        }

        return ValidatePlaces(items.Sum(WantedCountOf), placesElsewhere: 0);
    }

    /// <summary>Name and wanted count of a single item (FR-005, FR-007, FR-010).</summary>
    public static WishError? ValidateItem(WishItemDraft item)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
        {
            return new WishError(ErrorCodes.ItemNameRequired);
        }

        if (item.Name.Length > WishItem.NameMaxLength)
        {
            return new WishError(ErrorCodes.ItemNameTooLong, WishItem.NameMaxLength);
        }

        var wanted = WantedCountOf(item);

        return wanted is < 1 or > WishItem.MaxWantedCount
            ? new WishError(ErrorCodes.WantedCountInvalid, WishItem.MaxWantedCount)
            : null;
    }

    /// <summary>
    /// Whether a wanted count may be set to <paramref name="newCount"/> (FR-032, FR-033, FR-010a).
    /// </summary>
    /// <param name="claimsOnItem">How many names the item already carries.</param>
    /// <param name="placesElsewhere">The wanted counts of the list's other items, added together.</param>
    /// <remarks>
    /// Lowering below the claims already made is refused rather than resolved. Accepting it would
    /// mean either leaving the item oversubscribed - a state that lies about capacity - or
    /// dropping somebody's claim without saying so.
    /// </remarks>
    public static WishError? ValidateItemCount(int newCount, int claimsOnItem, int placesElsewhere)
    {
        if (newCount is < 1 or > WishItem.MaxWantedCount)
        {
            return new WishError(ErrorCodes.WantedCountInvalid, WishItem.MaxWantedCount);
        }

        if (newCount < claimsOnItem)
        {
            return new WishError(ErrorCodes.CountBelowEntries, claimsOnItem);
        }

        return ValidatePlaces(newCount, placesElsewhere);
    }

    /// <summary>FR-010a: the capacity is met where the operator works, never by a participant.</summary>
    public static WishError? ValidatePlaces(int places, int placesElsewhere) =>
        placesElsewhere + places > WishList.MaxPlaces
            ? new WishError(ErrorCodes.TooManyPlaces, WishList.MaxPlaces - placesElsewhere)
            : null;

    private static string Normalise(string name) => name.Trim();

    /// <summary>
    /// Creates a wish list owned by <paramref name="owner"/>, or by the operator when that is null
    /// (009 FR-011, FR-012, FR-023).
    /// </summary>
    /// <remarks>
    /// As with a poll, the owner is the whole of what feature 009 adds: the limits, the item
    /// ordering, the token and the absence of any expiry are unchanged, so an Ersteller's wish list
    /// is an ordinary wish list in every other respect (009 FR-014). Ownership is written once and
    /// never transfers (009 FR-012).
    /// </remarks>
    public async Task<WishList> CreateAsync(
        string title, string? description, DateOnly targetDate,
        IReadOnlyList<WishItemDraft> items, CancellationToken ct, Guid? owner = null)
    {
        var list = new WishList
        {
            Id = Guid.CreateVersion7(),
            Title = Normalise(title),
            Description = string.IsNullOrWhiteSpace(description) ? null : Normalise(description),
            TargetDate = targetDate,
            ListToken = CapabilityToken.Mint(),
            CreatedAt = clock.Now,
            CreatorId = owner,
            Items = [.. items.Select((item, position) => new WishItem
            {
                Id = Guid.CreateVersion7(),
                Name = Normalise(item.Name!),
                WantedCount = WantedCountOf(item),
                // FR-009: the order the operator entered them in, kept explicitly rather than
                // left to whatever order a query happens to return.
                Position = position,
            })],
        };

        db.WishLists.Add(list);
        await db.SaveChangesAsync(ct);

        // Identifiers and counts only - never a title, a name or a token (Principle IV).
        logger.LogInformation(
            "Wish list created {WishListId} with {ItemCount} items and {PlaceCount} places",
            list.Id, list.Items.Count, list.Items.Sum(i => i.WantedCount));

        return list;
    }

    /// <summary>A list with its items and claims, or null. The only way in for the admin area.</summary>
    public Task<WishList?> FindAsync(Guid id, CancellationToken ct) =>
        db.WishLists
            .Include(l => l.Items.OrderBy(i => i.Position))
            .ThenInclude(i => i.Claims)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

    /// <summary>
    /// FR-029: title, description and target date, changed at any time for as long as the list
    /// exists. The token is untouched (FR-035) and no claim is removed (FR-036).
    /// </summary>
    /// <remarks>
    /// Moving the target date is also how a closed list is reopened (FR-028c). There is no
    /// separate open/close control, because there is no stored state for one to set.
    /// </remarks>
    public async Task<(WishList? List, WishError? Error)> UpdateAsync(
        WishList list, string? title, string? description, DateOnly? targetDate,
        bool descriptionGiven, CancellationToken ct)
    {
        var newTitle = title ?? list.Title;
        var newDescription = descriptionGiven ? description : list.Description;
        var newTarget = targetDate ?? list.TargetDate;

        if (string.IsNullOrWhiteSpace(newTitle))
        {
            return (null, new WishError(ErrorCodes.TitleRequired));
        }

        if (newTitle.Length > WishList.TitleMaxLength)
        {
            return (null, new WishError(ErrorCodes.TitleTooLong, WishList.TitleMaxLength));
        }

        if (newDescription is { Length: > WishList.DescriptionMaxLength })
        {
            return (null, new WishError(ErrorCodes.DescriptionTooLong, WishList.DescriptionMaxLength));
        }

        list.Title = Normalise(newTitle);
        list.Description = string.IsNullOrWhiteSpace(newDescription) ? null : Normalise(newDescription);
        list.TargetDate = newTarget;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Wish list updated {WishListId}", list.Id);

        return (list, null);
    }

    /// <summary>FR-030: a new item starts with its full wanted count open.</summary>
    public async Task<(WishList? List, WishError? Error)> AddItemAsync(
        WishList list, WishItemDraft draft, CancellationToken ct)
    {
        var error = ValidateItem(draft);
        if (error is not null)
        {
            return (null, error);
        }

        var collision = list.Items.FirstOrDefault(
            i => string.Equals(i.Name, Normalise(draft.Name!), StringComparison.OrdinalIgnoreCase));

        if (collision is not null)
        {
            return (null, new WishError(ErrorCodes.DuplicateItemName, Detail: collision.Name));
        }

        if (list.Items.Count + 1 > WishList.MaxItems)
        {
            return (null, new WishError(ErrorCodes.TooManyItems, WishList.MaxItems));
        }

        var placesError = ValidatePlaces(WantedCountOf(draft), list.Items.Sum(i => i.WantedCount));
        if (placesError is not null)
        {
            return (null, placesError);
        }

        // Added through the set rather than through the navigation. The navigation was loaded by
        // a filtered, ordered Include, and appending to such a collection left the new row in a
        // state the change tracker treated as an update of something that does not exist yet
        // ("expected to affect 1 row, actually affected 0"). Naming the set removes the ambiguity.
        db.WishItems.Add(new WishItem
        {
            Id = Guid.CreateVersion7(),
            WishListId = list.Id,
            Name = Normalise(draft.Name!),
            WantedCount = WantedCountOf(draft),
            // Appended, so the order stays the one the operator built (FR-009).
            Position = list.Items.Count == 0 ? 0 : list.Items.Max(i => i.Position) + 1,
        });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Wish item added to wish list {WishListId}", list.Id);

        // Re-read, so the caller renders the list as it now stands rather than as the tracked
        // graph happens to look.
        return (await FindAsync(list.Id, ct), null);
    }

    /// <summary>
    /// FR-031 to FR-033: rename an item, or change how many of it are wanted. Renaming keeps every
    /// claim; lowering the count below the claims already made is refused, naming them.
    /// </summary>
    public async Task<(WishList? List, WishError? Error)> UpdateItemAsync(
        WishList list, Guid itemId, string? name, int? wantedCount, CancellationToken ct)
    {
        var item = list.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
        {
            return (null, new WishError(ErrorCodes.UnknownItem));
        }

        if (name is not null)
        {
            var draft = new WishItemDraft(name, item.WantedCount);
            var error = ValidateItem(draft);
            if (error is not null)
            {
                return (null, error);
            }

            var collision = list.Items.FirstOrDefault(other => other.Id != item.Id
                && string.Equals(other.Name, Normalise(name), StringComparison.OrdinalIgnoreCase));

            if (collision is not null)
            {
                return (null, new WishError(ErrorCodes.DuplicateItemName, Detail: collision.Name));
            }

            // The claims are not touched: this is the same item under a new name (FR-031).
            item.Name = Normalise(name);
        }

        if (wantedCount is not null)
        {
            var error = ValidateItemCount(
                wantedCount.Value,
                claimsOnItem: item.Claims.Count,
                placesElsewhere: list.Items.Where(i => i.Id != item.Id).Sum(i => i.WantedCount));

            if (error is not null)
            {
                return (null, error);
            }

            item.WantedCount = wantedCount.Value;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Wish item updated {WishItemId}", itemId);

        return (list, null);
    }

    /// <summary>
    /// FR-034: removes an item and the claims on it. What it destroys is stated by the interface
    /// before this is called; here it simply happens.
    /// </summary>
    public async Task<bool> RemoveItemAsync(Guid wishListId, Guid itemId, CancellationToken ct)
    {
        var removed = await db.WishItems
            .Where(i => i.Id == itemId && i.WishListId == wishListId)
            .ExecuteDeleteAsync(ct);

        if (removed > 0)
        {
            logger.LogInformation("Wish item removed {WishItemId} from wish list {WishListId}",
                itemId, wishListId);
        }

        return removed > 0;
    }

    /// <summary>
    /// FR-037 to FR-039: the only thing that removes a wish list. Cascades to items and claims,
    /// and afterwards every token issued for it behaves like an unknown one.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var removed = await db.WishLists.Where(l => l.Id == id).ExecuteDeleteAsync(ct);

        if (removed > 0)
        {
            logger.LogInformation("Wish list deleted {WishListId}", id);
        }

        return removed > 0;
    }
}
