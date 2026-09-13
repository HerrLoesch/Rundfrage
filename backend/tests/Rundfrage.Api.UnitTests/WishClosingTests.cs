using Rundfrage.Api.Time;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// When a wish list closes (008 FR-028a, FR-028c).
/// </summary>
/// <remarks>
/// A wish list has no status column: closedness is the comparison below, evaluated on every
/// access. These tests are what make that comparison reproducible instead of dependent on the
/// machine's zone - and the summer-time cases are why the end of a day cannot be "the date plus
/// 24 hours" computed once.
/// </remarks>
public class WishClosingTests
{
    private static BerlinClock ClockAt(DateTimeOffset instant) => new(new FixedTimeProvider(instant));

    private static bool IsClosed(BerlinClock clock, DateOnly targetDate) =>
        clock.Now > clock.EndOfDayUtc(targetDate);

    [Fact]
    public void A_winter_day_ends_at_23_00_UTC()
    {
        // 15 January ends at 00:00 Berlin on the 16th, which is 23:00 UTC on the 15th.
        var clock = ClockAt(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(
            new DateTime(2026, 1, 15, 23, 0, 0, DateTimeKind.Utc),
            clock.EndOfDayUtc(new DateOnly(2026, 1, 15)));
    }

    [Fact]
    public void A_summer_day_ends_at_22_00_UTC()
    {
        // Two hours ahead rather than one - the reason this is resolved per instant (FR-011b).
        var clock = ClockAt(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(
            new DateTime(2026, 7, 15, 22, 0, 0, DateTimeKind.Utc),
            clock.EndOfDayUtc(new DateOnly(2026, 7, 15)));
    }

    [Fact]
    public void The_day_summer_time_begins_is_an_hour_shorter()
    {
        // Clocks go forward at 02:00 on 29 March 2026. The day still ends at 00:00 on the 30th,
        // which is 22:00 UTC - an hour earlier than the winter rule would give.
        var clock = ClockAt(new DateTimeOffset(2026, 3, 29, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(
            new DateTime(2026, 3, 29, 22, 0, 0, DateTimeKind.Utc),
            clock.EndOfDayUtc(new DateOnly(2026, 3, 29)));
    }

    [Fact]
    public void A_list_is_open_at_the_last_second_of_its_target_day()
    {
        // 23:59:59 Berlin on the target day is 22:59:59 UTC in winter.
        var clock = ClockAt(new DateTimeOffset(2026, 1, 15, 22, 59, 59, TimeSpan.Zero));

        Assert.False(IsClosed(clock, new DateOnly(2026, 1, 15)));
    }

    [Fact]
    public void A_list_is_closed_at_the_first_second_of_the_next_day()
    {
        var clock = ClockAt(new DateTimeOffset(2026, 1, 15, 23, 0, 1, TimeSpan.Zero));

        Assert.True(IsClosed(clock, new DateOnly(2026, 1, 15)));
    }

    [Fact]
    public void A_target_date_in_the_future_leaves_the_list_open()
    {
        var clock = ClockAt(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

        Assert.False(IsClosed(clock, new DateOnly(2026, 7, 18)));
    }

    [Fact]
    public void Moving_the_target_date_forward_reopens_a_closed_list()
    {
        // FR-028c: there is no state to repair, because there is no state. The same clock gives
        // both answers for two different target dates.
        var clock = ClockAt(new DateTimeOffset(2026, 1, 20, 12, 0, 0, TimeSpan.Zero));

        Assert.True(IsClosed(clock, new DateOnly(2026, 1, 15)));
        Assert.False(IsClosed(clock, new DateOnly(2026, 1, 25)));
    }

    [Fact]
    public void The_retention_deadline_still_lands_thirty_days_after_the_end_of_the_day()
    {
        // RetentionDeadlineFor is refactored onto EndOfDayUtc; this pins that the refactoring
        // changed no value (002 FR-039).
        var clock = ClockAt(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(
            clock.EndOfDayUtc(new DateOnly(2026, 10, 15)) + BerlinClock.RetentionPeriod,
            clock.RetentionDeadlineFor(new DateOnly(2026, 10, 15)));
    }

    /// <summary>Local copy of the seam <see cref="BerlinClockTests"/> uses, for the same reason.</summary>
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
