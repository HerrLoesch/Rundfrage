namespace Rundfrage.Api.Http;

/// <summary>A refusal carrying the machine-readable code from the contract.</summary>
/// <remarks>
/// Each feature keeps its own error record (<c>PollError</c>, <c>WishError</c>, <c>CreatorError</c>,
/// <c>FormError</c>), so a service's signature still says which feature's refusals it returns;
/// this is only the shape they share on the way out.
/// </remarks>
public interface IRefusal
{
    string Code { get; }

    int? Limit { get; }

    string? Detail { get; }
}

/// <summary>
/// The one 400 response. Matches the <c>Problem</c> schema in contracts/openapi.yaml, written once
/// instead of as an anonymous object at every endpoint that refuses something.
/// </summary>
public static class Refusal
{
    public static IResult BadRequest(IRefusal error) =>
        Results.BadRequest(new { code = error.Code, limit = error.Limit, detail = error.Detail });

    /// <summary>A refusal that is only a code - a malformed request has no limit to name.</summary>
    public static IResult BadRequest(string code) => Results.BadRequest(new { code });
}
