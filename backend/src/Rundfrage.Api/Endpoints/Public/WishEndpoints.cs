using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Http;
using Rundfrage.Api.Time;
using Rundfrage.Api.Wishes;

namespace Rundfrage.Api.Endpoints.Public;

/// <summary>
/// Everything a participant reaches on a wish list. No session, no account, no email - the token
/// in the path is the whole of the authorisation (Principle I, 008 FR-013, FR-014).
/// </summary>
/// <remarks>
/// Two behaviours are inherited rather than written here, which is the point of how they were
/// built: maintenance mode is a middleware that intercepts everything under /api that is not the
/// admin group or health, and the neutral 404 is one payload for unknown, malformed and deleted
/// alike. Both are asserted in the integration suite anyway, because wiring is what regresses
/// (research R-13).
/// </remarks>
public static class WishEndpoints
{
    public sealed record ClaimRequest(string? DisplayName, Guid[]? ItemIds);

    public static IEndpointRouteBuilder MapWishEndpoints(this IEndpointRouteBuilder routes)
    {
        // Deliberately no shape check before the lookup: rejecting a malformed token early would
        // make it measurably faster than an unknown one (002 research R-4).
        routes.MapGet("/wish-lists/{listToken}", async (
            string listToken,
            RundfrageDbContext db,
            WishListProjection projection,
            CancellationToken ct) =>
        {
            var list = await LoadAsync(db, listToken, ct);

            return list is null
                ? NeutralNotFound.Result()
                : Results.Ok(projection.Participant(list));
        })
        .AllowAnonymous()
        .WithName("getWishListByToken");

        routes.MapPost("/wish-lists/{listToken}", async (
            string listToken,
            ClaimRequest request,
            RundfrageDbContext db,
            ClaimService claims,
            WishListProjection projection,
            CancellationToken ct) =>
        {
            var list = await LoadAsync(db, listToken, ct);

            if (list is null)
            {
                return NeutralNotFound.Result();
            }

            var (accepted, refusal) = await claims.ClaimAsync(
                list, request.DisplayName, request.ItemIds ?? [], ct);

            if (refusal is not null)
            {
                var payload = new
                {
                    code = refusal.Code,
                    item = refusal.ItemId is null
                        ? null
                        : new { id = refusal.ItemId, openPlaces = refusal.OpenPlaces },
                };

                // 409 for "there is no room", the same status 002 answers for poll_full; every
                // other refusal is a defect in the request itself.
                return refusal.Code == ErrorCodes.ItemFull
                    ? Results.Json(payload, statusCode: StatusCodes.Status409Conflict)
                    : Results.BadRequest(payload);
            }

            // Re-read, so the page is answered with the state its own claim produced.
            var updated = await LoadAsync(db, listToken, ct);

            return Results.Created($"/api/v1/claims/{accepted!.ClaimToken}", new
            {
                claimToken = accepted.ClaimToken,
                list = projection.Participant(updated!),
            });
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimiting.SubmissionPolicy)
        .WithName("claimWishItems");

        routes.MapGet("/claims/{claimToken}", async (
            string claimToken, ClaimService claims, BerlinClock clock, CancellationToken ct) =>
        {
            var covered = await claims.CoveredByAsync(claimToken, ct);

            if (covered.Count == 0)
            {
                // Unknown, malformed, withdrawn to nothing, or the list is gone (FR-022e) - all
                // one payload.
                return NeutralNotFound.Result();
            }

            var list = covered[0].WishItem!.WishList!;

            return Results.Ok(new
            {
                listTitle = list.Title,
                targetDate = list.TargetDate,
                closed = clock.DayHasEnded(list.TargetDate),
                entries = covered.Select(c => new
                {
                    claimId = c.Id,
                    itemName = c.WishItem!.Name,
                    displayName = c.DisplayName,
                }),
            });
        })
        .AllowAnonymous()
        .WithName("getClaims");

        routes.MapDelete("/claims/{claimToken}/{claimId:guid}", async (
            string claimToken, Guid claimId, ClaimService claims, CancellationToken ct) =>
        {
            var (removed, refusal) = await claims.WithdrawAsync(claimToken, claimId, ct);

            if (refusal is not null)
            {
                return Results.BadRequest(new { code = refusal.Code });
            }

            return removed ? Results.NoContent() : NeutralNotFound.Result();
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimiting.SubmissionPolicy)
        .WithName("withdrawClaim");

        return routes;
    }

    /// <summary>
    /// The one way in from a participant token. There is no retention filter here and there must
    /// not be one: a wish list stays reachable until the operator deletes it (FR-028, FR-037).
    /// </summary>
    private static Task<Data.Entities.WishList?> LoadAsync(
        RundfrageDbContext db, string listToken, CancellationToken ct) =>
        db.WishLists
            .Include(l => l.Items.OrderBy(i => i.Position))
            .ThenInclude(i => i.Claims)
            .FirstOrDefaultAsync(l => l.ListToken == listToken, ct);
}
