namespace Rundfrage.Api.Endpoints;

/// <summary>
/// GET /api/v1/message (FR-006). Returns a placeholder text with no product meaning; its only
/// purpose is to prove the page receives data from the backend at runtime (FR-007).
/// </summary>
public static class MessageEndpoint
{
    /// <summary>
    /// The value the page displays. Changing it here changes what the page shows, with no
    /// frontend change - which is exactly what FR-007 requires.
    /// </summary>
    private const string Message = "Rundfrage läuft.";

    public static IEndpointRouteBuilder MapMessageEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/message", () => Results.Ok(new MessageResponse(Message)))
              .WithName("getMessage");

        return routes;
    }
}

/// <summary>Matches MessageResponse in contracts/openapi.yaml.</summary>
public sealed record MessageResponse(string Message);
