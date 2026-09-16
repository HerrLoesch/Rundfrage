using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Creators;
using Rundfrage.Api.Endpoints.Admin;
using Rundfrage.Api.Http;
using Rundfrage.Api.Polls;
using Rundfrage.Api.Wishes;

namespace Rundfrage.Api.Endpoints.Creator;

/// <summary>
/// Everything an Ersteller reaches through their link (009 FR-022 to FR-028g).
/// </summary>
/// <remarks>
/// <b>No session, no account, no password.</b> The token in the path is the whole of the
/// authorisation, exactly as it is for a participant - the constitution permits that for creators
/// and this feature goes further than it has to by not asking for a password either.
/// <para>
/// <b>Mounted beside the participant routes and deliberately NOT under <c>/api/v1/admin</c>.</b>
/// That prefix is the one <c>MaintenanceMiddleware</c> exempts, so mounting here would silently
/// hand a bearer link the right to write into a database that a restore is about to replace.
/// <c>CreatorMaintenanceTests</c> is the guard, and it is expected to pass on its first run
/// (009 FR-050, research R-6).
/// </para>
/// <para>
/// <b>Every handler receives <see cref="OwnerScope"/> and none receives
/// <c>RundfrageDbContext</c>.</b> There is no unscoped queryable within reach, so a handler that
/// forgets the filter has nothing to call - which is what makes 009 FR-033 structural rather than a
/// review comment. A cross-owner miss answers the same neutral payload an unknown id answers, never
/// a 403: "not yours" and "no such thing" must be indistinguishable (FR-034, SC-003).
/// </para>
/// </remarks>
public static class CreatorEndpoints
{
    /// <summary>
    /// Resolves the token once per request and binds the scope, or refuses neutrally.
    /// </summary>
    /// <remarks>
    /// One filter on the group rather than a lookup per handler, for the reason the admin group
    /// records for <c>RequireAuthorization</c>: a per-handler check is a rule somebody eventually
    /// forgets to repeat on a route added later.
    /// <para>
    /// There is deliberately no shape check before the lookup. Rejecting a malformed token early
    /// would make it measurably faster than an unknown one, and telling those apart is precisely
    /// what the neutral refusal denies (002 research R-4).
    /// </para>
    /// </remarks>
    private sealed class ResolveCreator : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(
            EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var token = context.HttpContext.Request.RouteValues["creatorToken"] as string;

            var creators = context.HttpContext.RequestServices.GetRequiredService<CreatorService>();
            var creator = await creators.ResolveAsync(token, context.HttpContext.RequestAborted);

            if (creator is null)
            {
                return NeutralNotFound.Result();
            }

            context.HttpContext.RequestServices.GetRequiredService<OwnerScope>().Bind(creator);

            return await next(context);
        }
    }

    public static IEndpointRouteBuilder MapCreatorEndpoints(this IEndpointRouteBuilder routes)
    {
        var creator = routes.MapGroup("/e/{creatorToken}")
            .AllowAnonymous()
            .AddEndpointFilter<ResolveCreator>();

        // The write budget is applied per endpoint rather than to the group, deliberately.
        // RequireRateLimiting on a RouteGroupBuilder adds a convention to the *whole* group, so a
        // "writes" sub-group derived from this one limited the reads as well - and FR-036c is
        // explicit that a holder refreshing their own list must never be refused. The per-endpoint
        // form is also what WishEndpoints uses for the submission policy.

        MapSurface(creator);
        MapPolls(creator);
        MapWishLists(creator);

        return routes;
    }

    /// <summary>
    /// FR-028d: one address, and everything the holder may see in one response.
    /// </summary>
    /// <remarks>
    /// There is no dashboard here and no figure across the two lists (FR-028b). Each list carries
    /// the per-poll and per-list figures features 002 and 008 already define, which is what pays
    /// for the absence of a deep link into either of them (FR-028g).
    /// </remarks>
    private static void MapSurface(RouteGroupBuilder creator) =>
        creator.MapGet("/", async (
            OwnerScope scope, WishListProjection wishLists, CancellationToken ct) =>
        {
            var polls = await scope.Polls()
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PollListItem(
                    p.Id, p.Title, p.ParticipantToken, p.RetentionDeadline,
                    p.Responses.Count, p.Days.Count))
                .ToListAsync(ct);

            var lists = await wishLists.ListAsync(ct, scope.WishLists());

            return Results.Ok(new
            {
                name = scope.Owner.Name,
                polls = polls.Select(PollSummary.From),
                wishLists = lists,
            });
        })
        .WithName("getCreatorSurface");

    private static void MapPolls(RouteGroupBuilder creator)
    {
        creator.MapPost("/polls", async (
            PollCreationRequest request, OwnerScope scope, PollService polls, CancellationToken ct) =>
        {
            var days = request.Days ?? [];

            // Feature 002's validation, unchanged. An Ersteller meets exactly the limits the
            // operator meets (FR-022).
            var error = PollService.Validate(request.Title, request.Message, days);
            if (error is not null)
            {
                return Results.BadRequest(new { code = error.Code, limit = error.Limit });
            }

            var poll = await polls.CreateAsync(
                request.Title!, request.Message, days, ct, owner: scope.Owner.Id);

            return Results.Created($"/api/v1/e/x/polls/{poll.Id}", PollSummary.FromCreated(poll));
        })
        .RequireRateLimiting(RateLimiting.CreatorWritePolicy)
        .WithName("createPollAsCreator");

        creator.MapGet("/polls/{pollId:guid}", async (
            Guid pollId, int? page, OwnerScope scope, ResultsProjection results, CancellationToken ct) =>
        {
            var poll = await scope.Polls().FirstOrDefaultAsync(p => p.Id == pollId, ct);

            return poll is null
                ? NeutralNotFound.Result()
                : Results.Ok(await results.BuildAsync(poll, page ?? 1, ct));
        })
        .WithName("getPollResultsAsCreator");

        // FR-028: the same document the operator receives for the same poll, because it is the
        // same builder. There is deliberately no import counterpart here or anywhere reachable
        // with a creator token (FR-028a).
        creator.MapGet("/polls/{pollId:guid}/export", async (
            Guid pollId, OwnerScope scope, PollExport export, CancellationToken ct) =>
        {
            var poll = await scope.Polls().FirstOrDefaultAsync(p => p.Id == pollId, ct);
            if (poll is null)
            {
                return NeutralNotFound.Result();
            }

            var document = await export.BuildAsync(poll, ct);
            var json = JsonSerializer.SerializeToUtf8Bytes(
                document, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            return Results.File(
                json,
                contentType: "application/json",
                fileDownloadName: PollExport.FileNameFor(poll.Title, document.ExportedAt));
        })
        .WithName("exportPollAsCreator");

        creator.MapDelete("/polls/{pollId:guid}", async (
            Guid pollId, OwnerScope scope, PollService polls, CancellationToken ct) =>
        {
            // Ownership is asked first, and the delete then goes through PollService rather than
            // a DbContext this handler does not have. The admin endpoint deletes inline, which is
            // safe there and would be a hole here (009 FR-033, research R-1).
            var poll = await scope.Polls().FirstOrDefaultAsync(p => p.Id == pollId, ct);
            if (poll is null)
            {
                return NeutralNotFound.Result();
            }

            return await polls.DeleteAsync(pollId, ct)
                ? Results.NoContent()
                : NeutralNotFound.Result();
        })
        .RequireRateLimiting(RateLimiting.CreatorWritePolicy)
        .WithName("deletePollAsCreator");

        creator.MapDelete("/polls/{pollId:guid}/responses/{responseId:guid}", async (
            Guid pollId, Guid responseId, OwnerScope scope, ResponseService responses,
            CancellationToken ct) =>
        {
            var poll = await scope.Polls().FirstOrDefaultAsync(p => p.Id == pollId, ct);
            if (poll is null)
            {
                return NeutralNotFound.Result();
            }

            return await responses.DeleteAsync(pollId, responseId, ct)
                ? Results.NoContent()
                : NeutralNotFound.Result();
        })
        .RequireRateLimiting(RateLimiting.CreatorWritePolicy)
        .WithName("deleteResponseAsCreator");
    }

    private static void MapWishLists(RouteGroupBuilder creator)
    {
        creator.MapPost("/wish-lists", async (
            WishListAdminEndpoints.WishListRequest request,
            OwnerScope scope,
            WishListService lists,
            WishListProjection projection,
            CancellationToken ct) =>
        {
            var drafts = (request.Items ?? [])
                .Select(i => new WishItemDraft(i.Name, i.WantedCount))
                .ToArray();

            var targetDate = DateOnly.TryParse(request.TargetDate, out var parsedTargetDate)
                ? parsedTargetDate
                : (DateOnly?)null;

            var error = WishListService.ValidateList(
                request.Title, request.Description, targetDate, drafts);

            if (error is not null)
            {
                return Results.BadRequest(new { code = error.Code, limit = error.Limit, detail = error.Detail });
            }

            var list = await lists.CreateAsync(
                request.Title!, request.Description, targetDate!.Value, drafts, ct,
                owner: scope.Owner.Id);

            return Results.Created($"/api/v1/e/x/wish-lists/{list.Id}", projection.Detail(list));
        })
        .RequireRateLimiting(RateLimiting.CreatorWritePolicy)
        .WithName("createWishListAsCreator");

        creator.MapGet("/wish-lists/{wishListId:guid}", async (
            Guid wishListId, OwnerScope scope, WishListService lists, WishListProjection projection,
            CancellationToken ct) =>
        {
            var list = await OwnedListAsync(scope, lists, wishListId, ct);

            return list is null ? NeutralNotFound.Result() : Results.Ok(projection.Detail(list));
        })
        .WithName("getWishListAsCreator");

        creator.MapPatch("/wish-lists/{wishListId:guid}", async (
            Guid wishListId, JsonElement body, OwnerScope scope, WishListService lists,
            WishListProjection projection, CancellationToken ct) =>
        {
            var list = await OwnedListAsync(scope, lists, wishListId, ct);
            if (list is null)
            {
                return NeutralNotFound.Result();
            }

            if (body.ValueKind != JsonValueKind.Object)
            {
                return Results.BadRequest(new { code = ErrorCodes.MalformedRequest });
            }

            WishListAdminEndpoints.WishListPatch? patch;
            try
            {
                patch = JsonSerializer.Deserialize<WishListAdminEndpoints.WishListPatch>(
                    body.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { code = ErrorCodes.MalformedRequest });
            }

            if (patch is null)
            {
                return Results.BadRequest(new { code = ErrorCodes.MalformedRequest });
            }

            var descriptionGiven = body.TryGetProperty("description", out _);

            var (updated, error) = await lists.UpdateAsync(
                list, patch.Title, patch.Description, patch.TargetDate, descriptionGiven, ct);

            return error is not null
                ? Results.BadRequest(new { code = error.Code, limit = error.Limit, detail = error.Detail })
                : Results.Ok(projection.Detail(updated!));
        })
        .RequireRateLimiting(RateLimiting.CreatorWritePolicy)
        .WithName("updateWishListAsCreator");

        creator.MapPost("/wish-lists/{wishListId:guid}/items", async (
            Guid wishListId, WishListAdminEndpoints.WishItemRequest request, OwnerScope scope,
            WishListService lists, WishListProjection projection, CancellationToken ct) =>
        {
            var list = await OwnedListAsync(scope, lists, wishListId, ct);
            if (list is null)
            {
                return NeutralNotFound.Result();
            }

            var (updated, error) = await lists.AddItemAsync(
                list, new WishItemDraft(request.Name, request.WantedCount), ct);

            return error is not null
                ? Results.BadRequest(new { code = error.Code, limit = error.Limit, detail = error.Detail })
                : Results.Created($"/api/v1/e/x/wish-lists/{wishListId}", projection.Detail(updated!));
        })
        .RequireRateLimiting(RateLimiting.CreatorWritePolicy)
        .WithName("addWishItemAsCreator");

        creator.MapPatch("/wish-lists/{wishListId:guid}/items/{itemId:guid}", async (
            Guid wishListId, Guid itemId, WishListAdminEndpoints.WishItemRequest request,
            OwnerScope scope, WishListService lists, WishListProjection projection,
            CancellationToken ct) =>
        {
            var list = await OwnedListAsync(scope, lists, wishListId, ct);
            if (list is null)
            {
                return NeutralNotFound.Result();
            }

            var (updated, error) = await lists.UpdateItemAsync(
                list, itemId, request.Name, request.WantedCount, ct);

            return error is not null
                ? Results.BadRequest(new { code = error.Code, limit = error.Limit, detail = error.Detail })
                : Results.Ok(projection.Detail(updated!));
        })
        .RequireRateLimiting(RateLimiting.CreatorWritePolicy)
        .WithName("updateWishItemAsCreator");

        creator.MapDelete("/wish-lists/{wishListId:guid}/items/{itemId:guid}", async (
            Guid wishListId, Guid itemId, OwnerScope scope, WishListService lists,
            CancellationToken ct) =>
        {
            if (await OwnedListAsync(scope, lists, wishListId, ct) is null)
            {
                return NeutralNotFound.Result();
            }

            return await lists.RemoveItemAsync(wishListId, itemId, ct)
                ? Results.NoContent()
                : NeutralNotFound.Result();
        })
        .RequireRateLimiting(RateLimiting.CreatorWritePolicy)
        .WithName("deleteWishItemAsCreator");

        creator.MapDelete("/wish-lists/{wishListId:guid}/claims/{claimId:guid}", async (
            Guid wishListId, Guid claimId, OwnerScope scope, WishListService lists,
            ClaimService claims, CancellationToken ct) =>
        {
            if (await OwnedListAsync(scope, lists, wishListId, ct) is null)
            {
                return NeutralNotFound.Result();
            }

            return await claims.DeleteAsync(wishListId, claimId, ct)
                ? Results.NoContent()
                : NeutralNotFound.Result();
        })
        .RequireRateLimiting(RateLimiting.CreatorWritePolicy)
        .WithName("deleteWishClaimAsCreator");

        creator.MapDelete("/wish-lists/{wishListId:guid}", async (
            Guid wishListId, OwnerScope scope, WishListService lists, CancellationToken ct) =>
        {
            if (await OwnedListAsync(scope, lists, wishListId, ct) is null)
            {
                return NeutralNotFound.Result();
            }

            return await lists.DeleteAsync(wishListId, ct)
                ? Results.NoContent()
                : NeutralNotFound.Result();
        })
        .RequireRateLimiting(RateLimiting.CreatorWritePolicy)
        .WithName("deleteWishListAsCreator");
    }

    /// <summary>
    /// A wish list this Ersteller owns, with its items and claims - or null, whether it belongs to
    /// somebody else or does not exist. The caller cannot tell those apart and neither may the
    /// response (FR-034).
    /// </summary>
    /// <remarks>
    /// Ownership is asked of <see cref="OwnerScope"/> and the aggregate is then loaded by
    /// <c>WishListService.FindAsync</c>, which is the one place that knows how to include the items
    /// in the operator's order and their claims. Duplicating that include here would be a second
    /// definition of what a wish list is.
    /// </remarks>
    private static async Task<Data.Entities.WishList?> OwnedListAsync(
        OwnerScope scope, WishListService lists, Guid wishListId, CancellationToken ct)
    {
        var owned = await scope.WishLists().AnyAsync(l => l.Id == wishListId, ct);

        return owned ? await lists.FindAsync(wishListId, ct) : null;
    }
}
