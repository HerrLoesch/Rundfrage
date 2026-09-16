using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Http;
using Rundfrage.Api.Security;
using Rundfrage.Api.Time;

namespace Rundfrage.Api.Wishes;

/// <summary>What a refused claim reports back, with the item's state where one applies.</summary>
public sealed record ClaimRefusal(string Code, Guid? ItemId = null, int? OpenPlaces = null);

/// <summary>A stored submission: the token that covers it, and the claims it created.</summary>
public sealed record ClaimAccepted(string ClaimToken, IReadOnlyList<Guid> ClaimIds);

/// <summary>
/// Claiming and withdrawing (008 FR-016 to FR-022e). Capacity lives here and nowhere else.
/// </summary>
public sealed class ClaimService(
    RundfrageDbContext db, BerlinClock clock, ILogger<ClaimService> logger)
{
    public static WishError? ValidateName(string? displayName) => displayName switch
    {
        null or "" => new WishError(ErrorCodes.DisplayNameRequired),
        _ when string.IsNullOrWhiteSpace(displayName) => new WishError(ErrorCodes.DisplayNameRequired),
        { Length: > WishClaim.DisplayNameMaxLength } =>
            new WishError(ErrorCodes.DisplayNameTooLong, WishClaim.DisplayNameMaxLength),
        _ => null,
    };

    /// <summary>
    /// Stores one submission's claims, or refuses it.
    /// </summary>
    /// <remarks>
    /// <b>Capacity is the transaction, not a check before one.</b> Counting the claims on an item
    /// and inserting into it happen inside a single <i>immediate</i> write transaction, so two
    /// submissions for the last place queue instead of both reading "one free" (FR-017a, SC-002).
    /// This is the shape <see cref="Rundfrage.Api.Polls.ResponseService"/> already uses for the
    /// response cap, and the reason it must be immediate rather than deferred is recorded there:
    /// a deferred transaction takes a read lock first, and the upgrade at the insert cannot wait.
    /// <para>
    /// Every claim of one submission is given the <i>same</i> freshly minted token. That sharing
    /// is the whole mechanism of the personal link, and it is why no submission table exists
    /// (FR-022b, research R-2).
    /// </para>
    /// </remarks>
    public async Task<(ClaimAccepted? Accepted, ClaimRefusal? Refusal)> ClaimAsync(
        WishList list, string? displayName, IReadOnlyList<Guid> itemIds, CancellationToken ct)
    {
        var nameError = ValidateName(displayName);
        if (nameError is not null)
        {
            return (null, new ClaimRefusal(nameError.Code));
        }

        if (itemIds.Count == 0)
        {
            return (null, new ClaimRefusal(ErrorCodes.UnknownItem));
        }

        // FR-028a: judged against the moment the request arrives, never against what the page
        // was showing (FR-028e).
        if (clock.DayHasEnded(list.TargetDate))
        {
            return (null, new ClaimRefusal(ErrorCodes.WishListClosed));
        }

        var itemsOfList = await db.WishItems
            .Where(i => i.WishListId == list.Id)
            .Select(i => new { i.Id, i.WantedCount })
            .ToDictionaryAsync(i => i.Id, i => i.WantedCount, ct);

        foreach (var itemId in itemIds)
        {
            // An item from another list would attach a name where it does not belong, so it is
            // refused rather than skipped.
            if (!itemsOfList.ContainsKey(itemId))
            {
                return (null, new ClaimRefusal(ErrorCodes.UnknownItem));
            }
        }

        await using var transaction = await SqliteWriteTransaction.BeginAsync(db, ct);

        var taken = await db.WishClaims
            .Where(c => itemIds.Contains(c.WishItemId))
            .GroupBy(c => c.WishItemId)
            .Select(g => new { ItemId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ItemId, g => g.Count, ct);

        var claims = new List<WishClaim>();
        var token = CapabilityToken.Mint();

        foreach (var itemId in itemIds)
        {
            var wanted = itemsOfList[itemId];

            // Counted per item across the whole submission, so claiming the same item twice in
            // one request cannot exceed its capacity either.
            var already = taken.GetValueOrDefault(itemId) + claims.Count(c => c.WishItemId == itemId);

            if (already >= wanted)
            {
                await transaction.RollbackAsync(ct);
                return (null, new ClaimRefusal(ErrorCodes.ItemFull, itemId, 0));
            }

            claims.Add(new WishClaim
            {
                Id = Guid.CreateVersion7(),
                WishItemId = itemId,
                DisplayName = displayName!.Trim(),
                ClaimToken = token,
                SubmittedAt = clock.Now,
            });
        }

        db.WishClaims.AddRange(claims);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        // Identifiers and counts only. Never the name, never the token.
        logger.LogInformation(
            "Claim submitted covering {ItemCount} items of wish list {WishListId}",
            claims.Count, list.Id);

        return (new ClaimAccepted(token, [.. claims.Select(c => c.Id)]), null);
    }

    /// <summary>Everything one personal link covers, ordered as the list presents its items.</summary>
    public async Task<IReadOnlyList<WishClaim>> CoveredByAsync(string claimToken, CancellationToken ct) =>
        await db.WishClaims
            .Include(c => c.WishItem!)
            .ThenInclude(i => i.WishList!)
            .Where(c => c.ClaimToken == claimToken)
            .OrderBy(c => c.WishItem!.Position)
            .ToListAsync(ct);

    /// <summary>
    /// FR-022, FR-022a: removes one entry the token covers and opens its place again. Refuses once
    /// the list has closed, because a place freed then can no longer be claimed by anybody
    /// (FR-028d) - after that only the operator can remove an entry (FR-043a).
    /// </summary>
    public async Task<(bool Removed, ClaimRefusal? Refusal)> WithdrawAsync(
        string claimToken, Guid claimId, CancellationToken ct)
    {
        var claim = await db.WishClaims
            .Include(c => c.WishItem!)
            .ThenInclude(i => i.WishList!)
            .FirstOrDefaultAsync(c => c.ClaimToken == claimToken && c.Id == claimId, ct);

        if (claim is null)
        {
            return (false, null);
        }

        if (clock.DayHasEnded(claim.WishItem!.WishList!.TargetDate))
        {
            return (false, new ClaimRefusal(ErrorCodes.WishListClosed));
        }

        db.WishClaims.Remove(claim);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Claim withdrawn {ClaimId}", claimId);

        return (true, null);
    }

    /// <summary>FR-043a: the operator removes one entry, whether the list is open or closed.</summary>
    public async Task<bool> DeleteAsync(Guid wishListId, Guid claimId, CancellationToken ct)
    {
        var removed = await db.WishClaims
            .Where(c => c.Id == claimId && c.WishItem!.WishListId == wishListId)
            .ExecuteDeleteAsync(ct);

        if (removed > 0)
        {
            logger.LogInformation(
                "Claim deleted {ClaimId} from wish list {WishListId}", claimId, wishListId);
        }

        return removed > 0;
    }
}
