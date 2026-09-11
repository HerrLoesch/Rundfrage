namespace Rundfrage.Api.Maintenance;

/// <summary>
/// Replaces the participant surface with a maintenance notice while maintenance is on
/// (FR-026, FR-027, FR-028, FR-031).
/// </summary>
/// <remarks>
/// <b>One gate rather than a check per endpoint.</b> FR-026 is a statement about *every*
/// participant route, including ones added later, and a per-handler check is a rule somebody
/// eventually forgets to repeat - the same reasoning that put <c>RequireAuthorization</c> on the
/// admin group instead of on each admin handler (002 FR-048).
/// <para>
/// The split, and why each branch is what it is:
/// </para>
/// <list type="bullet">
/// <item><c>/api/v1/admin/**</c> passes untouched (FR-028). Without this the operator locks
/// themselves out of the switch that ends the maintenance window.</item>
/// <item><c>/api/v1/health</c> passes untouched (FR-031). Maintenance is a deliberate state, not a
/// fault; failing the check would have the orchestrator replace or roll back the deployment while
/// the operator is working.</item>
/// <item>Every other API route answers the notice. Reads and writes alike, which is what makes
/// FR-027 true without a second rule: a submission never reaches its handler, so nothing can
/// record an answer and then report success.</item>
/// <item>Non-API paths pass through to the page shell, so the application loads and renders the
/// notice. It discloses nothing on its own - the shell is the same bytes for every address - and
/// serving a separate static page here would mean a second copy of the notice to keep in step
/// with the one the application already owns.</item>
/// </list>
/// <para>
/// The answer is byte-identical whatever token was asked for (FR-032). A different reply for a
/// real poll than for an invented one would tell an outsider that a poll is behind that link -
/// exactly the distinction 002 SC-012's neutral 404 exists to deny.
/// </para>
/// </remarks>
public sealed class MaintenanceMiddleware(RequestDelegate next)
{
    private const string ApiPrefix = "/api/";
    private const string AdminPrefix = "/api/v1/admin";
    private const string HealthPath = "/api/v1/health";

    public async Task InvokeAsync(HttpContext context, MaintenanceState state)
    {
        if (!Intercepts(context.Request.Path) || !state.IsOn)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        // No Retry-After: the operator has not told anybody when they will be finished, and a
        // number invented here would be a promise the system cannot keep.
        await context.Response.WriteAsJsonAsync(new { code = "maintenance" });
    }

    private static bool Intercepts(PathString path) =>
        path.StartsWithSegments(ApiPrefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
        && !path.StartsWithSegments(AdminPrefix, StringComparison.OrdinalIgnoreCase)
        && !path.Equals(HealthPath, StringComparison.OrdinalIgnoreCase);
}

public static class MaintenanceMiddlewareExtensions
{
    /// <summary>
    /// Registers the gate. Placed before routing so that a route added later is covered by it
    /// without anybody remembering to say so (FR-033).
    /// </summary>
    public static IApplicationBuilder UseMaintenanceMode(this IApplicationBuilder app) =>
        app.UseMiddleware<MaintenanceMiddleware>();
}
