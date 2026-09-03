using Rundfrage.Api.Data;
using Rundfrage.Api.Time;

namespace Rundfrage.Api.Endpoints.Admin;

/// <summary>Downloading a consistent copy of the storage (FR-003).</summary>
public static class BackupEndpoint
{
    public static IEndpointRouteBuilder MapBackupEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/backup", async (
            BackupService backups, BerlinClock clock, ILogger<BackupService> logger, CancellationToken ct) =>
        {
            string path;

            try
            {
                path = await backups.CreateAsync(ct);
            }
            catch (Exception ex)
            {
                // FR-024: storage being unreachable costs this one request, not the application.
                // Type only, never the path or the exception object (002 FR-026).
                logger.LogError("A backup could not be produced ({Detail})", ex.GetType().Name);

                return Results.Json(
                    new { code = "storage_unavailable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            // DeleteOnClose is what makes FR-021 true without a cleanup job: the file is gone
            // when the response finishes, whether it finished by being sent or by being
            // abandoned halfway. A file deleted after an awaited copy would survive the second
            // case, and those are the ones that accumulate.
            var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.None,
                bufferSize: 64 * 1024, FileOptions.DeleteOnClose | FileOptions.Asynchronous);

            return Results.File(
                stream,
                contentType: "application/octet-stream",
                fileDownloadName: BackupService.FileNameFor(clock.Now));
        });

        return routes;
    }
}
