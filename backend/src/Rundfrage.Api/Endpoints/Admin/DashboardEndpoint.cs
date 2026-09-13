using Rundfrage.Api.Polls;

namespace Rundfrage.Api.Endpoints.Admin;

/// <summary>The dashboard's figures (007 FR-028).</summary>
/// <remarks>
/// One route and one read. Six separate endpoints - one per figure - would have meant six round
/// trips against SC-011's budget and six chances for a partial failure to produce a dashboard that
/// is half figures and half error.
/// <para>
/// The session requirement is inherited: the admin group already carries RequireAuthorization(),
/// so this route needs no attribute of its own (002 FR-048).
/// </para>
/// </remarks>
public static class DashboardEndpoint
{
    public static IEndpointRouteBuilder MapDashboardEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/dashboard", async (DashboardProjection dashboard, CancellationToken ct) =>
            Results.Ok(await dashboard.BuildAsync(ct)))
            .WithName("getDashboard");

        return routes;
    }
}
