using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Http;
using Rundfrage.Api.Polls;
using Rundfrage.Api.Retention;

namespace Rundfrage.Api.Endpoints.Public;

/// <summary>Submitting and revising an answer, without an account (FR-025 to FR-031).</summary>
public static class ResponseEndpoints
{
    public static IEndpointRouteBuilder MapResponseEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/polls/{pollToken}/responses", async (
            string pollToken,
            SubmissionRequest request,
            RetentionService retention,
            ResponseService responses,
            CancellationToken ct) =>
        {
            var poll = await retention.LivePolls()
                .FirstOrDefaultAsync(p => p.ParticipantToken == pollToken, ct);

            if (poll is null)
            {
                return NeutralNotFound.Result();
            }

            var result = await responses.SubmitAsync(
                poll, request.DisplayName, request.Answers ?? [], ct);

            if (result.Error is { } error)
            {
                // The cap is a conflict, not bad input: the answer was well formed and the poll
                // is simply full (FR-015a).
                return error.Code == "poll_full"
                    ? Results.Json(new { code = error.Code, limit = error.Limit },
                        statusCode: StatusCodes.Status409Conflict)
                    : Results.BadRequest(new { code = error.Code, limit = error.Limit });
            }

            // The only way back to this answer, because no account exists to look it up with.
            return Results.Created(
                $"/api/v1/responses/{result.Response!.EditToken}",
                new SubmissionAccepted(result.Response.Id, result.Response.EditToken));
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimiting.SubmissionPolicy)
        .WithName("submitResponse");

        routes.MapGet("/responses/{editToken}", async (
            string editToken,
            RundfrageDbContext db,
            RetentionService retention,
            ResultsProjection results,
            CancellationToken ct) =>
        {
            var found = await LoadOwnAsync(db, retention, editToken, ct);
            if (found is null)
            {
                return NeutralNotFound.Result();
            }

            var (response, poll) = found.Value;

            return Results.Ok(new OwnResponse(
                response.Id,
                response.DisplayName,
                [.. response.Answers.Select(a => new AnswerView(a.CandidateDayId, ResultsProjection.ToToken(a.Availability)))],
                await results.BuildAsync(poll, 1, ct)));
        })
        .AllowAnonymous()
        .WithName("getOwnResponse");

        routes.MapPut("/responses/{editToken}", async (
            string editToken,
            SubmissionRequest request,
            RundfrageDbContext db,
            RetentionService retention,
            ResponseService responses,
            ResultsProjection results,
            CancellationToken ct) =>
        {
            var found = await LoadOwnAsync(db, retention, editToken, ct);
            if (found is null)
            {
                return NeutralNotFound.Result();
            }

            var (response, poll) = found.Value;

            var result = await responses.ReviseAsync(
                response, request.DisplayName, request.Answers ?? [], ct);

            if (result.Error is { } error)
            {
                return Results.BadRequest(new { code = error.Code, limit = error.Limit });
            }

            return Results.Ok(new OwnResponse(
                response.Id,
                response.DisplayName,
                [.. response.Answers.Select(a => new AnswerView(a.CandidateDayId, ResultsProjection.ToToken(a.Availability)))],
                await results.BuildAsync(poll, 1, ct)));
        })
        .AllowAnonymous()
        .WithName("reviseOwnResponse");

        return routes;
    }

    /// <summary>
    /// Resolves an edit token to its response and poll, honouring the retention deadline so an
    /// expired poll's personal links stop working at the same instant (FR-040).
    /// </summary>
    private static async Task<(Data.Entities.PollResponse Response, Data.Entities.Poll Poll)?> LoadOwnAsync(
        RundfrageDbContext db, RetentionService retention, string editToken, CancellationToken ct)
    {
        var response = await db.Responses
            .Include(r => r.Answers)
            .FirstOrDefaultAsync(r => r.EditToken == editToken, ct);

        if (response is null)
        {
            return null;
        }

        var poll = await retention.LivePolls().FirstOrDefaultAsync(p => p.Id == response.PollId, ct);

        return poll is null ? null : (response, poll);
    }
}

public sealed record SubmissionRequest(string? DisplayName, SubmittedAnswer[]? Answers);

public sealed record SubmissionAccepted(Guid ResponseId, string EditToken);

public sealed record OwnResponse(
    Guid ResponseId, string DisplayName, IReadOnlyList<AnswerView> Answers, PollView Poll);
