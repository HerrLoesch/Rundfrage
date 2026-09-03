namespace Rundfrage.Api.Security;

/// <summary>
/// Slows guessing against the single operator account: 5 consecutive failures lock it for 15
/// minutes (FR-005).
/// </summary>
/// <remarks>
/// In-memory state. There is one account and one instance, so there is nothing to share, and a
/// table for two integers would be storage the feature does not need (research.md R-12).
/// <para>
/// A restart clears the lockout. That is recorded rather than solved: an attacker who can
/// restart the process has already taken the machine.
/// </para>
/// </remarks>
public sealed class SignInThrottle(TimeProvider timeProvider)
{
    public const int MaxFailures = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly Lock _gate = new();
    private int _consecutiveFailures;
    private DateTimeOffset? _lockedUntil;

    public bool IsLocked(out TimeSpan retryAfter)
    {
        lock (_gate)
        {
            ClearElapsedLockout();

            if (_lockedUntil is { } until)
            {
                retryAfter = until - timeProvider.GetUtcNow();
                return true;
            }

            retryAfter = TimeSpan.Zero;
            return false;
        }
    }

    public void RecordFailure()
    {
        lock (_gate)
        {
            ClearElapsedLockout();

            if (++_consecutiveFailures >= MaxFailures)
            {
                _lockedUntil = timeProvider.GetUtcNow() + LockoutDuration;
            }
        }
    }

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _consecutiveFailures = 0;
            _lockedUntil = null;
        }
    }

    /// <summary>
    /// A lockout ends by elapsing, never by being lifted: FR-005a rules out an unlock function,
    /// because with a single account there is nobody to authorise one.
    /// </summary>
    /// <remarks>
    /// One place, called by both entry points. It previously existed twice - once inline in
    /// <see cref="IsLocked"/> and once in a private helper with an unused <c>out</c> parameter
    /// that was always set to zero and always discarded.
    /// </remarks>
    private void ClearElapsedLockout()
    {
        if (_lockedUntil is { } until && timeProvider.GetUtcNow() >= until)
        {
            _lockedUntil = null;
            _consecutiveFailures = 0;
        }
    }
}
