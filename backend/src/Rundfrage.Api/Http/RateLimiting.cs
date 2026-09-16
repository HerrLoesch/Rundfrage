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

    /// <summary>
    /// 009 FR-036: changes made through an Ersteller link, at most sixty per hour per source.
    /// </summary>
    /// <remarks>
    /// <b>A separate policy, not a share of the participant budget.</b> Ten per hour is right for a
    /// participant, who submits once; it would refuse an Ersteller halfway through adding the tenth
    /// item to the first wish list they were invited to make. Sixty is the number the specification
    /// fixes; a policy is simply where the framework keeps it.
    /// <para>
    /// Partitions are held per policy, so this budget and the participant's are separate buckets
    /// for the same address - an operator answering their own poll cannot spend an Ersteller's
    /// allowance. That is a framework behaviour this code depends on and does not own, so
    /// <c>CreatorRateLimitTests</c> asserts it (research R-5).
    /// </para>
    /// <para>
    /// Reads are deliberately not limited by it (FR-036c): a holder refreshing their own list must
    /// never be refused.
    /// </para>
    /// </remarks>
    public const string CreatorWritePolicy = "creator-writes";

    /// <summary>The value 009 FR-036 requires, and the default when nothing is configured.</summary>
    public const int DefaultCreatorWritesPerWindow = 60;

    public const string CreatorWritesVariable = "CREATOR_WRITE_LIMIT_PER_HOUR";

    /// <summary>
    /// Configurable, for the same reason the submission limit is: an end-to-end run legitimately
    /// makes more changes from one machine in an hour than any person would.
    /// </summary>
    public static int CreatorWritesPerWindow(IConfiguration configuration) =>
        int.TryParse(configuration[CreatorWritesVariable], out var configured) && configured > 0
            ? configured
            : DefaultCreatorWritesPerWindow;

    public static void AddSubmissionRateLimiter(
        this IServiceCollection services, IConfiguration configuration)
    {
        var permits = PermitsPerWindow(configuration);
        var creatorWrites = CreatorWritesPerWindow(configuration);

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

            options.AddPolicy(CreatorWritePolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = creatorWrites,
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
