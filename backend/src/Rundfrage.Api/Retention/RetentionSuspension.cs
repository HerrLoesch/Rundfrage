namespace Rundfrage.Api.Retention;

/// <summary>
/// Holds the retention sweep off while something needs the storage to itself (research R-2).
/// </summary>
/// <remarks>
/// <b>This exists because of a measurement, not a requirement.</b> A restore replaces the live
/// database through SQLite's backup mechanism, and that mechanism fails outright if any connection
/// holds an open transaction:
/// <code>
/// restore during open read tx : FAILED SqliteException: SQLite Error 5: 'database is locked'
/// </code>
/// Maintenance mode locks participants out (FR-024), which removes every *request* that could hold
/// one. It does not remove <see cref="RetentionSweep"/>, which is not a request: it wakes every
/// hour and opens a transaction regardless of maintenance mode. Without this gate a restore fails
/// intermittently, depending on nothing more than what time the operator pressed the button — and
/// the operator would see a failure with no cause they could act on.
/// <para>
/// A plain async mutex rather than a cancellation of the sweep: the sweep may already be inside its
/// transaction when the restore arrives, and what the restore needs is to <em>wait for it to
/// finish</em>, which is exactly what waiting on the gate does.
/// </para>
/// </remarks>
public sealed class RetentionSuspension
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Takes the storage. Dispose the result to give it back.</summary>
    public async Task<IDisposable> AcquireAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        return new Release(_gate);
    }

    /// <summary>
    /// Takes the storage if it is free, without waiting. The sweep uses this: its work can always
    /// wait an hour, and blocking it behind a restore would only queue a sweep that then runs
    /// against data the operator has just replaced.
    /// </summary>
    public IDisposable? TryAcquire() => _gate.Wait(0) ? new Release(_gate) : null;

    private sealed class Release(SemaphoreSlim gate) : IDisposable
    {
        private bool _released;

        public void Dispose()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            gate.Release();
        }
    }
}
