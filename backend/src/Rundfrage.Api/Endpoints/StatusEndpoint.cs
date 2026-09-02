using Rundfrage.Api.Diagnostics;

namespace Rundfrage.Api.Endpoints;

/// <summary>
/// GET /api/v1/status/database (FR-009). Always answers 200 while the application is running,
/// including when the database is unreachable: the frontend derives its third UI state from
/// any non-2xx response, so a 503 here would render a database outage as a backend outage
/// (research.md R-4, contracts/openapi.yaml).
/// </summary>
public static class StatusEndpoint
{
    public static IEndpointRouteBuilder MapStatusEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/status/database", async (DatabaseProbe probe, CancellationToken ct) =>
              {
                  var status = await probe.CheckAsync(ct);
                  return Results.Ok(new DatabaseStatusResponse(
                      status.State == DatabaseState.Reachable ? "reachable" : "unreachable",
                      status.CheckedAt,
                      status.DurationMs));
              })
              .WithName("getDatabaseStatus");

        return routes;
    }
}

/// <summary>
/// Matches DatabaseStatusResponse in contracts/openapi.yaml. Language-neutral by design: the
/// state is a token, and the frontend maps it to a translation key (FR-029, research.md R-9).
/// </summary>
public sealed record DatabaseStatusResponse(string State, DateTimeOffset CheckedAt, int DurationMs);
