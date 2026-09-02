using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;

namespace Rundfrage.Api.Diagnostics;

/// <summary>
/// Determines whether the database is reachable by executing a trivial scalar query through
/// Entity Framework Core (FR-008). The query depends on no application table, which is why
/// FR-013a covers schema creation with a separate test.
/// </summary>
public sealed class DatabaseProbe(RundfrageDbContext db, ILogger<DatabaseProbe> logger)
{
    /// <summary>FR-012. Also bounded by Timeout=2 in the connection string (research.md R-3).</summary>
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    public async Task<ConnectivityStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(Budget);

        var stopwatch = Stopwatch.StartNew();
        DatabaseState state;
        string outcomeDetail;

        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1", budget.Token);
            state = DatabaseState.Reachable;
            outcomeDetail = "ok";
        }
        catch (Exception ex)
        {
            state = DatabaseState.Unreachable;

            // Only the exception *type* is recorded. Npgsql messages can carry connection
            // detail, and the exception object is deliberately not attached to the log entry,
            // so nothing can leak through its ToString() either (FR-026, research.md R-5).
            outcomeDetail = ex is OperationCanceledException && !cancellationToken.IsCancellationRequested
                ? "TimeoutException"
                : ex.GetType().Name;
        }

        stopwatch.Stop();
        var durationMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue);

        // Exactly one entry per check, carrying outcome and duration (FR-027, SC-011).
        logger.LogInformation(
            "Database check finished: {Outcome} ({Detail}) in {DurationMs} ms",
            state == DatabaseState.Reachable ? "Reachable" : "Unreachable",
            outcomeDetail,
            durationMs);

        return new ConnectivityStatus(state, DateTimeOffset.UtcNow, durationMs);
    }
}
