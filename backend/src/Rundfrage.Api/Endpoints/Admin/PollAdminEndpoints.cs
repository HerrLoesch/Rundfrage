using System.Text.Json;
using Rundfrage.Api.Data;
using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Http;
using Rundfrage.Api.Polls;
using Rundfrage.Api.Retention;

namespace Rundfrage.Api.Endpoints.Admin;

/// <summary>Creating, listing, reading, exporting and deleting polls.</summary>
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

        routes.MapGet("/polls/{pollId:guid}", async (
            Guid pollId,
            int? page,
            RetentionService retention,
            ResultsProjection results,
            CancellationToken ct) =>
        {
            var poll = await retention.LivePolls().FirstOrDefaultAsync(p => p.Id == pollId, ct);

            // The same neutral payload the participant routes use: the admin area adds the poll
            // list and deletion, not privileged knowledge of what exists (FR-002).
            return poll is null
                ? NeutralNotFound.Result()
                : Results.Ok(await results.BuildAsync(poll, page ?? 1, ct));
        })
        .WithName("getPollResults");

        // FR-013: one poll as JSON, creator only - the group requirement covers it, as it
        // covers every other admin route (002 FR-048).
        routes.MapGet("/polls/{pollId:guid}/export", async (
            Guid pollId, RetentionService retention, PollExport export, CancellationToken ct) =>
        {
            var poll = await retention.LivePolls()
                .FirstOrDefaultAsync(p => p.Id == pollId, ct);

            if (poll is null)
            {
                return NeutralNotFound.Result();
            }

            var document = await export.BuildAsync(poll, ct);

            // Serialised here rather than returned as an object, because this is a download and
            // not an API response: Results.Json cannot set the file name FR-021a asks for, and
            // Results.File can. Web defaults so the document uses the same casing as everything
            // else the system emits - an export that disagreed with the API about how a field is
            // spelled would be a second contract to keep in step.
            var json = JsonSerializer.SerializeToUtf8Bytes(
                document, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            return Results.File(
                json,
                contentType: "application/json",
                fileDownloadName: PollExport.FileNameFor(poll.Title, document.ExportedAt));
        })
        .WithName("exportPoll");

        routes.MapDelete("/polls/{pollId:guid}", async (
            Guid pollId,
            RundfrageDbContext db,
            RetentionService retention,
            ILogger<PollService> logger,
            CancellationToken ct) =>
        {
            var poll = await retention.LivePolls().FirstOrDefaultAsync(p => p.Id == pollId, ct);
            if (poll is null)
            {
                return NeutralNotFound.Result();
            }

            // Cascades to days and responses. A real delete, not a flag - Principle IV requires
            // the responses to be removed rather than hidden (FR-037).
            await db.Polls.Where(p => p.Id == pollId).ExecuteDeleteAsync(ct);

            logger.LogInformation("Poll deleted {PollId}", pollId);

            return Results.NoContent();
        })
        .WithName("deletePoll");

        routes.MapDelete("/polls/{pollId:guid}/responses/{responseId:guid}", async (
            Guid pollId,
            Guid responseId,
            RetentionService retention,
            ResponseService responses,
            CancellationToken ct) =>
        {
            var poll = await retention.LivePolls().FirstOrDefaultAsync(p => p.Id == pollId, ct);
            if (poll is null)
            {
                return NeutralNotFound.Result();
            }

            // FR-037a: one response, leaving the poll and every other response untouched.
            var deleted = await responses.DeleteAsync(pollId, responseId, ct);

            return deleted ? Results.NoContent() : NeutralNotFound.Result();
        })
        .WithName("deleteResponse");

        return routes;
    }
}

public sealed record PollCreationRequest(string? Title, string? Message, DateOnly[]? Days);

/// <summary>Matches PollSummary in contracts/openapi.yaml.</summary>
public sealed record PollSummary(
    Guid Id,
    string Title,
    string ParticipantToken,
    DateTime RetentionDeadline,
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
