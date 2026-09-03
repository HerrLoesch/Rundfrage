using System.Threading.RateLimiting;

namespace Rundfrage.Api.Http;

/// <summary>
/// FR-027a: at most 10 submissions per hour per request source, so a leaked link cannot be used
/// to fill a poll to its 1000-response limit.
/// </summary>
/// <remarks>
/// The partition key is the request source, held in memory for the length of the window and
/// written nowhere. FR-027b and FR-042 forbid persisting it, and the built-in limiter satisfies
/// that by construction rather than by anyone remembering not to save it (research.md R-5).
/// <para>
/// A restart clears the windows. That is accepted: persisting the counters would mean persisting
/// the request source, which is precisely what the requirement prohibits. The limit is a speed
/// bump against abuse, not a security boundary.
/// </para>
/// </remarks>
public static class RateLimiting
{
    public const string SubmissionPolicy = "submissions";

    /// <summary>The value FR-027a requires, and the default when nothing is configured.</summary>
    public const int DefaultPermitsPerWindow = 10;

    public const string PermitsVariable = "SUBMISSION_LIMIT_PER_HOUR";

    public static readonly TimeSpan Window = TimeSpan.FromHours(1);

    /// <summary>
    /// Configurable, defaulting to the ten of FR-027a.
    /// </summary>
    /// <remarks>
    /// The end-to-end suite legitimately submits far more than ten answers per hour from one
    /// machine, and would otherwise start failing halfway through a run - which is exactly what
    /// happened. Making the number configurable lets a test environment raise it explicitly
    /// while an unconfigured deployment still gets the limit the specification requires.
    /// </remarks>
    public static int PermitsPerWindow(IConfiguration configuration) =>
        int.TryParse(configuration[PermitsVariable], out var configured) && configured > 0
            ? configured
            : DefaultPermitsPerWindow;

    public static void AddSubmissionRateLimiter(
        this IServiceCollection services, IConfiguration configuration)
    {
        var permits = PermitsPerWindow(configuration);

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(SubmissionPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permits,
                        Window = Window,
                        QueueLimit = 0,
                    }));

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value)
                    ? (int)value.TotalSeconds
                    : (int)Window.TotalSeconds;

                // FR-027c: say when to try again, and never accept-then-discard the answer.
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { code = "too_many_requests", retryAfterSeconds = retryAfter }, token);
            };
        });
    }
}
