using Rundfrage.Api.Security;

namespace Rundfrage.Api.UnitTests;

/// <summary>FR-005 and FR-005a: 5 failures lock the single account for 15 minutes.</summary>
public class SignInThrottleTests
{
    private static (SignInThrottle Throttle, MutableTimeProvider Clock) Build()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero));
        return (new SignInThrottle(clock), clock);
    }

    [Fact]
    public void Four_failures_do_not_lock()
    {
        var (throttle, _) = Build();

        for (var i = 0; i < 4; i++)
        {
            throttle.RecordFailure();
        }

        Assert.False(throttle.IsLocked(out _));
    }

    [Fact]
    public void The_fifth_failure_locks_for_fifteen_minutes()
    {
        var (throttle, _) = Build();

        for (var i = 0; i < 5; i++)
        {
            throttle.RecordFailure();
        }

        Assert.True(throttle.IsLocked(out var retryAfter));
        Assert.InRange(retryAfter.TotalMinutes, 14.9, 15.0);
    }

    [Fact]
    public void The_lockout_expires_on_its_own()
    {
        // FR-005a: no unlock function and no reset path, because there is no second account to
        // authorise one.
        var (throttle, clock) = Build();
        for (var i = 0; i < 5; i++)
        {
            throttle.RecordFailure();
        }

        clock.Advance(TimeSpan.FromMinutes(15).Add(TimeSpan.FromSeconds(1)));

        Assert.False(throttle.IsLocked(out _));
    }

    [Fact]
    public void A_success_resets_the_failure_count()
    {
        var (throttle, _) = Build();
        for (var i = 0; i < 4; i++)
        {
            throttle.RecordFailure();
        }

        throttle.RecordSuccess();
        for (var i = 0; i < 4; i++)
        {
            throttle.RecordFailure();
        }

        Assert.False(throttle.IsLocked(out _));
    }

    [Fact]
    public void Failures_after_a_lockout_expires_start_over()
    {
        var (throttle, clock) = Build();
        for (var i = 0; i < 5; i++)
        {
            throttle.RecordFailure();
        }

        clock.Advance(TimeSpan.FromMinutes(16));
        throttle.RecordFailure();

        Assert.False(throttle.IsLocked(out _));
    }

    private sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
