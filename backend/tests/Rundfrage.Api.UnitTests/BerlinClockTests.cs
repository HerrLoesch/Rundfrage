using Rundfrage.Api.Time;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// FR-011a: every day boundary resolves against Europe/Berlin. FR-011b: summer time is handled.
/// These are the tests that make date behaviour reproducible instead of dependent on where the
/// suite happens to run.
/// </summary>
public class BerlinClockTests
{
    private static BerlinClock ClockAt(DateTimeOffset instant) => new(new FixedTimeProvider(instant));

    [Fact]
    public void Just_after_midnight_in_winter_is_already_the_new_day()
    {
        // 00:30 Berlin on 15 January is 23:30 UTC on 14 January. Against UTC the date would be
        // wrong by a day, which is precisely what FR-011a exists to prevent.
        var clock = ClockAt(new DateTimeOffset(2026, 1, 14, 23, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 1, 15), clock.Today);
    }

    [Fact]
    public void Just_after_midnight_in_summer_is_already_the_new_day()
    {
        // 00:30 Berlin on 15 July is 22:30 UTC on 14 July - a two-hour offset, not one.
        var clock = ClockAt(new DateTimeOffset(2026, 7, 14, 22, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 7, 15), clock.Today);
    }

    [Fact]
    public void Just_before_midnight_is_still_the_old_day()
    {
        var clock = ClockAt(new DateTimeOffset(2026, 1, 14, 22, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 1, 14), clock.Today);
    }

    [Theory]
    [InlineData(2026, 1, 14, 2026, 1, 15, false)]  // day after today -> not past
    [InlineData(2026, 1, 14, 2026, 1, 14, false)]  // today -> not past
    [InlineData(2026, 1, 14, 2026, 1, 13, true)]   // yesterday -> past
    public void Determines_whether_a_day_is_past_against_Berlin(
        int y, int m, int d, int dy, int dm, int dd, bool expectedPast)
    {
        // FR-014 permits past days; it still has to know which they are.
        var clock = ClockAt(new DateTimeOffset(y, m, d, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(expectedPast, clock.IsPast(new DateOnly(dy, dm, dd)));
    }

    [Fact]
    public void Retention_deadline_is_thirty_days_after_the_end_of_the_last_day()
    {
        // FR-039: the last candidate day ends at 23:59:59 Berlin; the deadline is 30 days later.
        var clock = ClockAt(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

        var deadline = clock.RetentionDeadlineFor(new DateOnly(2026, 10, 15));

        // 15 Oct 24:00 Berlin (= 16 Oct 00:00, CEST, UTC+2) plus 30 days.
        Assert.Equal(new DateTimeOffset(2026, 11, 14, 22, 0, 0, TimeSpan.Zero), deadline);
    }

    [Fact]
    public void Retention_deadline_stays_correct_across_a_summer_time_change()
    {
        // FR-011b. A last day in late October puts the deadline in November, after the
        // changeover: the day ends at UTC+2 but the deadline lands in UTC+1.
        var clock = ClockAt(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

        var deadline = clock.RetentionDeadlineFor(new DateOnly(2026, 10, 20));

        Assert.Equal(new DateTimeOffset(2026, 11, 19, 22, 0, 0, TimeSpan.Zero), deadline);
    }

    [Fact]
    public void A_poll_whose_deadline_has_passed_is_expired()
    {
        var clock = ClockAt(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.True(clock.HasPassed(new DateTimeOffset(2026, 11, 30, 23, 59, 59, TimeSpan.Zero)));
        Assert.False(clock.HasPassed(new DateTimeOffset(2026, 12, 1, 0, 0, 1, TimeSpan.Zero)));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
