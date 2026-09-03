namespace Rundfrage.Api.Http;

/// <summary>
/// The single not-found response. Returned identically whether a token is unknown, malformed,
/// expired, or belongs to a deleted poll (FR-027, FR-040, SC-012).
/// </summary>
/// <remarks>
/// There is deliberately no overload taking a reason. A reason parameter is how the four cases
/// would drift apart, and telling them apart is exactly what the requirement forbids: an
/// attacker who can distinguish "expired" from "never existed" learns which tokens once existed.
/// <para>
/// For the same reason no endpoint answers 410 Gone for an expired poll. It would be more
/// honest HTTP and precisely the disclosure SC-012 denies.
/// </para>
/// </remarks>
public static class NeutralNotFound
{
    /// <summary>The one payload. A bare code - no message, no cause, no detail.</summary>
    public static readonly NotFoundPayload Payload = new("not_found");

    public static IResult Result() => Results.Json(Payload, statusCode: StatusCodes.Status404NotFound);

    /// <summary>Matches the <c>Problem</c> schema in contracts/openapi.yaml.</summary>
    public sealed record NotFoundPayload(
        [property: System.Text.Json.Serialization.JsonPropertyName("code")] string Code);
}
