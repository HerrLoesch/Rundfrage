namespace Rundfrage.Api.Time;

/// <summary>
/// The single authority on what a calendar day means (FR-011a). Everything that compares a day
/// to "now", decides whether a day is past, or computes a retention deadline goes through here.
/// </summary>
/// <remarks>
/// A day only becomes ambiguous the moment it meets a clock, which is why the specification's
/// original claim to exclude time zones could not hold. Fixing one zone here means a candidate
/// day denotes the same day for every participant, wherever they open the link.
/// <para>
/// Requires zone data in the runtime image. The Alpine image does not ship it - see
/// research.md R-6 and the guard in <c>e2e/tests/timezone.spec.ts</c>.
/// </para>
/// </remarks>
public sealed class BerlinClock(TimeProvider timeProvider)
{
    public const string ZoneId = "Europe/Berlin";

    /// <summary>How long a poll outlives its last candidate day (FR-039).</summary>
    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    private static readonly TimeZoneInfo Zone = ResolveZone();

    /// <summary>
    /// Resolved once, with the failure explained rather than wrapped.
    /// </summary>
    /// <remarks>
    /// A throwing static field initializer surfaces as <c>TypeInitializationException: The type
    /// initializer for 'BerlinClock' threw an exception</c>, which buries the cause and points
    /// at the wrong thing. The real problem is a runtime image without zone data, and the fix is
    /// one line in the Dockerfile - so the message says so.
    /// </remarks>
    private static TimeZoneInfo ResolveZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ZoneId);
        }
        catch (TimeZoneNotFoundException ex)
        {
            throw new InvalidOperationException(
                $"Time zone '{ZoneId}' is unavailable. The Alpine runtime image ships no zone data; "
                + "add `apk add --no-cache tzdata` to the runtime stage of docker/Dockerfile.", ex);
        }
    }

    /// <summary>
    /// The current instant, in UTC. Exposed so callers need no second time source.
    /// </summary>
    /// <remarks>
    /// A <see cref="DateTime"/> rather than a <see cref="DateTimeOffset"/> because this value is
    /// compared against stored instants, and the storage provider translates neither a comparison
    /// nor an ordering on <c>DateTimeOffset</c> (003 research.md R-1). Nothing is lost: every
    /// instant here was always UTC, so the offset carried no information the domain had.
    /// </remarks>
    public DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>Today's date as the group experiences it, not as UTC would report it.</summary>
    public DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Zone).DateTime);

    /// <summary>FR-014: past days are permitted, so something has to decide which they are.</summary>
    public bool IsPast(DateOnly day) => day < Today;

    /// <summary>
    /// The instant <paramref name="day"/> ends - midnight at its end, in this zone, as UTC.
    /// </summary>
    /// <remarks>
    /// One expression for "when does this day end", used by both things that need it: a poll's
    /// retention deadline (002 FR-039) and whether a wish list has closed (008 FR-028a). Two
    /// copies of this arithmetic would be two chances to get a summer-time boundary wrong in
    /// different ways.
    /// <para>
    /// Resolved per instant rather than as a fixed offset, so the day that summer time begins is
    /// an hour shorter and the day it ends an hour longer, which is what FR-011b asks for.
    /// </para>
    /// </remarks>
    public DateTime EndOfDayUtc(DateOnly day)
    {
        var endOfDay = day.ToDateTime(TimeOnly.MinValue).AddDays(1);

        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(endOfDay, DateTimeKind.Unspecified), Zone);
    }

    /// <summary>
    /// 008 FR-028a: a wish list closes once its target day has ended. Derived here and stored
    /// nowhere - there is no status column, so there is nothing that can be stale (FR-028c).
    /// </summary>
    public bool DayHasEnded(DateOnly day) => Now > EndOfDayUtc(day);

    /// <summary>
    /// FR-039: the instant 30 days after the end of <paramref name="lastCandidateDay"/>, where the
    /// day ends at 23:59:59 in this zone. Resolving the offset per instant rather than once means
    /// a summer-time change between the day and the deadline is handled (FR-011b).
    /// </summary>
    public DateTime RetentionDeadlineFor(DateOnly lastCandidateDay) =>
        EndOfDayUtc(lastCandidateDay) + RetentionPeriod;

    /// <summary>
    /// FR-039b: expiry takes effect on access, so this is asked on every read rather than being
    /// written into a status column that would be wrong until a job caught up.
    /// </summary>
    public bool HasPassed(DateTime deadline) => Now > deadline;
}
