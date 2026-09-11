using Rundfrage.Api.Maintenance;

namespace Rundfrage.Api.Endpoints.Admin;

/// <summary>Reading and setting maintenance state (FR-025, FR-033).</summary>
public static class MaintenanceEndpoints
{
    /// <summary>What the admin area is told. The moment is informational; `enabled` is the state.</summary>
    public sealed record MaintenanceView(bool Enabled, DateTime? Since);

    public sealed record MaintenanceRequest(bool Enabled);

    public static IEndpointRouteBuilder MapMaintenanceEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/maintenance", (MaintenanceState state) =>
            Results.Ok(new MaintenanceView(state.IsOn, state.Since)))
            .WithName("readMaintenance");

        routes.MapPut("/maintenance", (MaintenanceRequest request, MaintenanceState state) =>
        {
            // Idempotent on purpose: switching on when already on, or off when already off, is a
            // no-op that reports success. The operator is describing the state they want, not
            // issuing a toggle whose result depends on what it was before.
            if (request.Enabled)
            {
                state.TurnOn();
            }
            else
            {
                state.TurnOff();
            }

            return Results.Ok(new MaintenanceView(state.IsOn, state.Since));
        })
        .WithName("setMaintenance");

        return routes;
    }
}
