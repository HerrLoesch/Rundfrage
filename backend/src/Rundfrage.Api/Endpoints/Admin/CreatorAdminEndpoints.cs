using Rundfrage.Api.Creators;
using Rundfrage.Api.Http;

namespace Rundfrage.Api.Endpoints.Admin;

/// <summary>Managing Ersteller (009 FR-010, FR-016 to FR-020c, FR-043 to FR-047).</summary>
/// <remarks>
/// The session requirement is inherited: the admin group carries <c>RequireAuthorization()</c>, so
/// no route here repeats it (002 FR-048).
/// <para>
/// <b>Revoking and deleting are different addresses, not one address with a flag.</b> Revoking
/// takes away the link and destroys nothing; deleting destroys the Ersteller together with every
/// poll and wish list it owns. 009 FR-020b requires that they cannot read as variants of one
/// another, and addressing the link as a sub-resource is what makes that true in the API as well as
/// in the interface: <c>DELETE .../link</c> plainly removes a link, <c>DELETE .../{id}</c> plainly
/// removes an Ersteller.
/// </para>
/// </remarks>
public static class CreatorAdminEndpoints
{
    public sealed record CreatorRequest(string? Name);

    public static IEndpointRouteBuilder MapCreatorAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/creators", async (CreatorProjection projection, CancellationToken ct) =>
            Results.Ok(await projection.ListAsync(ct)))
            .WithName("listCreators");

        routes.MapPost("/creators", async (
            CreatorRequest request,
            CreatorService creators,
            CreatorProjection projection,
            CancellationToken ct) =>
        {
            var (created, error) = await creators.CreateAsync(request.Name, ct);

            return error is not null
                ? Results.BadRequest(new { code = error.Code, limit = error.Limit, detail = error.Detail })
                : Results.Created(
                    $"/api/v1/admin/creators/{created!.Id}", await projection.FindAsync(created.Id, ct));
        })
        .WithName("createCreator");

        routes.MapPatch("/creators/{creatorId:guid}", async (
            Guid creatorId,
            CreatorRequest request,
            CreatorService creators,
            CreatorProjection projection,
            CancellationToken ct) =>
        {
            var creator = await creators.FindAsync(creatorId, ct);
            if (creator is null)
            {
                return NeutralNotFound.Result();
            }

            var (_, error) = await creators.RenameAsync(creator, request.Name, ct);

            return error is not null
                ? Results.BadRequest(new { code = error.Code, limit = error.Limit, detail = error.Detail })
                : Results.Ok(await projection.FindAsync(creatorId, ct));
        })
        .WithName("renameCreator");

        // FR-016. Also the way a revoked Ersteller is given access again: there is no "unrevoke",
        // because revocation is the absence of a token and issuing one is its opposite.
        routes.MapPost("/creators/{creatorId:guid}/link", async (
            Guid creatorId,
            CreatorService creators,
            CreatorProjection projection,
            CancellationToken ct) =>
                await creators.ReissueAsync(creatorId, ct) is null
                    ? NeutralNotFound.Result()
                    : Results.Ok(await projection.FindAsync(creatorId, ct)))
            .WithName("reissueCreatorLink");

        // FR-018 and FR-020: the link stops working and NOTHING it owns is removed, altered or
        // reassigned. Everything that Ersteller made stays listed under its name.
        routes.MapDelete("/creators/{creatorId:guid}/link", async (
            Guid creatorId, CreatorService creators, CancellationToken ct) =>
                await creators.RevokeAsync(creatorId, ct)
                    ? Results.NoContent()
                    : NeutralNotFound.Result())
            .WithName("revokeCreatorLink");

        // FR-020a: destroys the Ersteller AND every poll and wish list it owns. The one route in
        // this file that removes anything beyond a link.
        routes.MapDelete("/creators/{creatorId:guid}", async (
            Guid creatorId, CreatorService creators, CancellationToken ct) =>
                await creators.DeleteAsync(creatorId, ct)
                    ? Results.NoContent()
                    : NeutralNotFound.Result())
            .WithName("deleteCreator");

        return routes;
    }
}
