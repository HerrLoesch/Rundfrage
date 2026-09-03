using Rundfrage.Api.Polls;

namespace Rundfrage.Api.Endpoints.Admin;

/// <summary>Creating and listing polls (FR-008 to FR-018).</summary>
public static class PollAdminEndpoints
{
    public static IEndpointRouteBuilder MapPollAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/polls", async (
            PollCreationRequest request, PollService polls, CancellationToken ct) =>
        {
            var days = request.Days ?? [];

            var error = PollService.Validate(request.Title, request.Message, days);
            if (error is not null)
            {
                return Results.BadRequest(new { code = error.Code, limit = error.Limit });
            }

            var poll = await polls.CreateAsync(request.Title!, request.Message, days, ct);

            return Results.Created($"/api/v1/admin/polls/{poll.Id}", PollSummary.FromCreated(poll));
        })
        .WithName("createPoll");

        routes.MapGet("/polls", async (PollService polls, CancellationToken ct) =>
        {
            var all = await polls.ListAsync(ct);
            return Results.Ok(all.Select(PollSummary.From));
        
        })
        .WithName("listPolls");

        return routes;
    }
}

public sealed record PollCreationRequest(string? Title, string? Message, DateOnly[]? Days);

/// <summary>Matches PollSummary in contracts/openapi.yaml.</summary>
public sealed record PollSummary(
    Guid Id,
    string Title,
    string ParticipantToken,
    DateTimeOffset RetentionDeadline,
    int ResponseCount,
    int DayCount)
{
    public static PollSummary From(PollListItem item) => new(
        item.Id,
        item.Title,
        item.ParticipantToken,
        item.RetentionDeadline,
        item.ResponseCount,
        item.DayCount);

    /// <summary>
    /// For a freshly created poll, where the entity is in hand and has no responses yet.
    /// </summary>
    public static PollSummary FromCreated(Data.Entities.Poll poll) => new(
        poll.Id,
        poll.Title,
        poll.ParticipantToken,
        poll.RetentionDeadline,
        ResponseCount: 0,
        poll.Days.Count);
}
