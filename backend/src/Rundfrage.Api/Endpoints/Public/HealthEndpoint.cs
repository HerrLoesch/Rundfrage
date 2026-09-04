namespace Rundfrage.Api.Endpoints.Public;

/// <summary>
/// Liveness, for the container runtime and for whatever deploys it.
/// </summary>
/// <remarks>
/// Without something to ask, a broken release is indistinguishable from a working one: the
/// container counts as running while it restart-loops on a missing <c>ADMIN_USER</c>, and the
/// deployment is reported as successful.
/// <para>
/// It deliberately does <em>not</em> touch the storage. FR-024 requires the application to start
/// and keep serving when its storage cannot be reached, so a check that reported storage would
/// have the orchestrator replace - or roll back - a container that is behaving exactly as
/// specified. Storage state belongs in the admin area, which is the place that can say it to
/// someone able to act on it.
/// </para>
/// <para>
/// It says nothing else either: no version, no counts, no configuration. The endpoint is
/// unauthenticated, so everything it returns is public.
/// </para>
/// </remarks>
public static class HealthEndpoint
{
    public static IEndpointRouteBuilder MapHealthEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/health", () => Results.Ok(new { status = "ok" }))
            .AllowAnonymous()
            .WithName("health");

        return routes;
    }
}
