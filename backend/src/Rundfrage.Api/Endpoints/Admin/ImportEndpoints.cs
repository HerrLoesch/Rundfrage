using Rundfrage.Api.Data;
using Rundfrage.Api.Http;
using Rundfrage.Api.Maintenance;
using Rundfrage.Api.Polls;

namespace Rundfrage.Api.Endpoints.Admin;

/// <summary>Reading exported data back in (FR-001, FR-002).</summary>
public static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/polls/import", async (
            HttpContext context, PollImport import, ILogger<PollImport> logger, CancellationToken ct) =>
        {
            // FR-005a: whatever happens below, the upload does not outlive this request.
            await using var upload = await ImportUpload.ReceiveAsync(context, ct);

            if (upload is null)
            {
                return Results.Json(
                    new { code = ErrorCodes.NotAValidExport }, statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                await using var content = File.OpenRead(upload.Path);
                var (refusal, summary) = await import.ImportAsync(content, ct);

                if (refusal is not null)
                {
                    return Results.Json(
                        new { code = refusal.Code, limit = refusal.Limit },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                return Results.Ok(summary);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 003 FR-024: storage being unreachable costs this request, not the application.
                // Type only, never the path or the document (FR-005).
                logger.LogError("An import could not be completed ({Detail})", ex.GetType().Name);

                return Results.Json(
                    new { code = "storage_unavailable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
        // The admin group already requires a session, and the session cookie is SameSite=Strict,
        // which is what removes forged-form CSRF here (002 research R-10). The built-in
        // antiforgery filter would additionally demand a token this API has never issued.
        .DisableAntiforgery()
        .WithName("importPoll");

        // --- Restoring a whole backup (FR-016 to FR-024) ---------------------------------
        //
        // Deliberately separate routes from the JSON import above, and named for what they do.
        // FR-001 requires that an operator cannot trigger a whole-system replacement while
        // believing they are adding a single poll; one endpoint with a mode flag would be exactly
        // that mistake waiting to happen.

        routes.MapPost("/restore/preview", async (
            HttpContext context, RestoreService restore, MaintenanceState maintenance,
            CancellationToken ct) =>
        {
            if (!maintenance.IsOn)
            {
                return Refused(ErrorCodes.MaintenanceRequired);
            }

            await using var upload = await ImportUpload.ReceiveAsync(context, ct);

            if (upload is null)
            {
                return Results.Json(
                    new { code = ErrorCodes.NotABackup }, statusCode: StatusCodes.Status400BadRequest);
            }

            var (refusal, preview) = await restore.PreviewAsync(upload.Path, ct);

            return refusal is not null
                ? Results.Json(new { code = refusal.Code }, statusCode: StatusCodes.Status400BadRequest)
                : Results.Ok(preview);
        })
        .DisableAntiforgery()
        .WithName("previewRestore");

        routes.MapPost("/restore", async (
            HttpContext context, RestoreService restore, MaintenanceState maintenance,
            ILogger<RestoreService> logger, CancellationToken ct) =>
        {
            // FR-024. Refused rather than warned about: requiring maintenance mode is what makes
            // a participant answering into data being replaced impossible, instead of a case to
            // handle after the fact.
            if (!maintenance.IsOn)
            {
                return Refused(ErrorCodes.MaintenanceRequired);
            }

            await using var upload = await ImportUpload.ReceiveAsync(context, ct);

            if (upload is null)
            {
                return Results.Json(
                    new { code = ErrorCodes.NotABackup }, statusCode: StatusCodes.Status400BadRequest);
            }

            // FR-018: the operator has seen what will be lost and said so.
            if (!ImportUpload.Confirmed(context))
            {
                return Results.Json(
                    new { code = ErrorCodes.ConfirmationRequired },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                var (refusal, summary) = await restore.RestoreAsync(upload.Path, ct);

                if (refusal is null)
                {
                    return Results.Ok(summary);
                }

                // storage_locked is a conflict, not a bad request: the file was fine and trying
                // again in a moment is the right advice.
                return refusal.Code == ErrorCodes.StorageLocked
                    ? Results.Json(new { code = refusal.Code }, statusCode: StatusCodes.Status409Conflict)
                    : Results.Json(new { code = refusal.Code }, statusCode: StatusCodes.Status400BadRequest);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // FR-021 held inside RestoreService: the previous data has been put back.
                logger.LogError("A restore could not be completed ({Detail})", ex.GetType().Name);

                return Results.Json(
                    new { code = "storage_unavailable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
        .DisableAntiforgery()
        .WithName("restore");

        return routes;
    }

    /// <summary>A refusal that replaced nothing, as a conflict rather than a bad request.</summary>
    private static IResult Refused(string code) =>
        Results.Json(new { code }, statusCode: StatusCodes.Status409Conflict);
}
