using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Http;
using Rundfrage.Api.Polls;
using Rundfrage.Api.Retention;

namespace Rundfrage.Api.Endpoints.Public;

/// <summary>
/// Everything a participant reaches. Nothing here requires a session, an account or an email -
/// the token in the path is the whole of the authorisation (Principle I, FR-019 to FR-021).
/// </summary>
public static class PollEndpoints
{
    public static IEndpointRouteBuilder MapPollEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/polls/{pollToken}", async (
            string pollToken,
            int? page,
            RetentionService retention,
            ResultsProjection results,
            CancellationToken ct) =>
        {
            // Deliberately no shape check before the lookup. Rejecting a malformed token early
            // would make it measurably faster than an unknown one, and telling those apart is
            // what SC-012 denies (research.md R-4).
            var poll = await retention.LivePolls()
                .FirstOrDefaultAsync(p => p.ParticipantToken == pollToken, ct);

            if (poll is null)
            {
                // Unknown, malformed, expired and deleted all arrive here, identically.
                return NeutralNotFound.Result();
            }

            return Results.Ok(await results.BuildAsync(poll, page ?? 1, ct));
        })
        .AllowAnonymous()
        .WithName("getPoll");

        return routes;
    }
}
