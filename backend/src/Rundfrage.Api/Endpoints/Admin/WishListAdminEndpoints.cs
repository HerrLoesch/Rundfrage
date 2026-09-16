using System.Text.Json;
using Rundfrage.Api.Http;
using Rundfrage.Api.Wishes;

namespace Rundfrage.Api.Endpoints.Admin;

/// <summary>Creating, listing, reading and deleting wish lists (008 FR-041 to FR-047).</summary>
/// <remarks>
/// The session requirement is inherited: the admin group carries RequireAuthorization(), so no
/// route here repeats it (002 FR-048).
/// <para>
/// A miss answers the same neutral payload the participant routes use. The admin area adds
/// listing and deletion, not privileged knowledge of what exists (002 FR-002).
/// </para>
/// </remarks>
public static class WishListAdminEndpoints
{
    public sealed record WishItemRequest(string? Name, int? WantedCount);

    // TargetDate is a raw string, not a DateOnly?: minimal API's automatic JSON binding throws an
    // unhandled BadHttpRequestException for anything it cannot parse as a date - including the
    // empty string the wish-list form sends when the field is left blank - which reaches the
    // client as a bodyless or stack-trace-bearing 400 rather than the structured
    // { code: "target_date_required" } this endpoint already knows how to say. Parsing it by hand
    // below folds "blank" and "unparsable" into the one refusal FR-002 already names.
    public sealed record WishListRequest(
        string? Title, string? Description, string? TargetDate, WishItemRequest[]? Items);

    /// <summary>
    /// A partial change (FR-029). Every property is optional, and an omitted one means "leave it
    /// as it is" - which is why <see cref="Description"/> cannot distinguish "not mentioned" from
    /// "set to nothing" on its own; the handler asks the raw document instead.
    /// </summary>
    public sealed record WishListPatch(string? Title, string? Description, DateOnly? TargetDate);

    public static IEndpointRouteBuilder MapWishListAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/wish-lists", async (
            WishListRequest request,
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
                request.Title!, request.Description, targetDate!.Value, drafts, ct);

            return Results.Created(
                $"/api/v1/admin/wish-lists/{list.Id}", projection.Detail(list));
        })
        .WithName("createWishList");

        routes.MapGet("/wish-lists", async (WishListProjection projection, CancellationToken ct) =>
            Results.Ok(await projection.ListAsync(ct)))
            .WithName("listWishLists");

        routes.MapGet("/wish-lists/{wishListId:guid}", async (
            Guid wishListId,
            WishListService lists,
            WishListProjection projection,
            CancellationToken ct) =>
        {
            var list = await lists.FindAsync(wishListId, ct);

            return list is null
                ? NeutralNotFound.Result()
                : Results.Ok(projection.Detail(list));
        })
        .WithName("getWishList");

        routes.MapPatch("/wish-lists/{wishListId:guid}", async (
            Guid wishListId,
            JsonElement body,
            WishListService lists,
            WishListProjection projection,
            CancellationToken ct) =>
        {
            var list = await lists.FindAsync(wishListId, ct);
            if (list is null)
            {
                return NeutralNotFound.Result();
            }

            // A patch is an object or it is nothing. `null`, `[]`, `5` and `"x"` are all valid
            // JSON and none of them is a change to make, so they are refused like any other
            // defective request - before TryGetProperty, which throws on anything but an object.
            if (body.ValueKind != JsonValueKind.Object)
            {
                return Results.BadRequest(new { code = ErrorCodes.MalformedRequest });
            }

            WishListPatch? patch;
            try
            {
                patch = JsonSerializer.Deserialize<WishListPatch>(
                    body.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (JsonException)
            {
                // A property of the right name but the wrong type - targetDate: "gestern".
                return Results.BadRequest(new { code = ErrorCodes.MalformedRequest });
            }

            if (patch is null)
            {
                return Results.BadRequest(new { code = ErrorCodes.MalformedRequest });
            }

            // Read from the raw document, because a null Description in the record means both
            // "clear it" and "not mentioned", and those are different instructions.
            var descriptionGiven = body.TryGetProperty("description", out _);

            var (updated, error) = await lists.UpdateAsync(
                list, patch.Title, patch.Description, patch.TargetDate, descriptionGiven, ct);

            return error is not null
                ? Results.BadRequest(new { code = error.Code, limit = error.Limit, detail = error.Detail })
                : Results.Ok(projection.Detail(updated!));
        })
        .WithName("updateWishList");

        routes.MapPost("/wish-lists/{wishListId:guid}/items", async (
            Guid wishListId,
            WishItemRequest request,
            WishListService lists,
            WishListProjection projection,
            CancellationToken ct) =>
        {
            var list = await lists.FindAsync(wishListId, ct);
            if (list is null)
            {
                return NeutralNotFound.Result();
            }

            var (updated, error) = await lists.AddItemAsync(
                list, new WishItemDraft(request.Name, request.WantedCount), ct);

            return error is not null
                ? Results.BadRequest(new { code = error.Code, limit = error.Limit, detail = error.Detail })
                : Results.Created(
                    $"/api/v1/admin/wish-lists/{wishListId}", projection.Detail(updated!));
        })
        .WithName("addWishItem");

        routes.MapPatch("/wish-lists/{wishListId:guid}/items/{itemId:guid}", async (
            Guid wishListId,
            Guid itemId,
            WishItemRequest request,
            WishListService lists,
            WishListProjection projection,
            CancellationToken ct) =>
        {
            var list = await lists.FindAsync(wishListId, ct);
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
        .WithName("updateWishItem");

        routes.MapDelete("/wish-lists/{wishListId:guid}/items/{itemId:guid}", async (
            Guid wishListId, Guid itemId, WishListService lists, CancellationToken ct) =>
                await lists.RemoveItemAsync(wishListId, itemId, ct)
                    ? Results.NoContent()
                    : NeutralNotFound.Result())
            .WithName("deleteWishItem");

        // FR-043a: one entry, whether the list is open or closed. This is also the only recourse
        // for a participant who has lost their personal link (FR-022d).
        routes.MapDelete("/wish-lists/{wishListId:guid}/claims/{claimId:guid}", async (
            Guid wishListId, Guid claimId, ClaimService claims, CancellationToken ct) =>
                await claims.DeleteAsync(wishListId, claimId, ct)
                    ? Results.NoContent()
                    : NeutralNotFound.Result())
            .WithName("deleteWishClaim");

        // FR-037: the only thing that removes a wish list. Nothing expires one, and nothing
        // sweeps one away (WishRetentionTests guards that).
        routes.MapDelete("/wish-lists/{wishListId:guid}", async (
            Guid wishListId, WishListService lists, CancellationToken ct) =>
                await lists.DeleteAsync(wishListId, ct)
                    ? Results.NoContent()
                    : NeutralNotFound.Result())
            .WithName("deleteWishList");

        return routes;
    }
}
