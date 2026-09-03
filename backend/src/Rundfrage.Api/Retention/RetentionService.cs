using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Time;

namespace Rundfrage.Api.Retention;

/// <summary>
/// Two separate concerns that FR-039 deliberately keeps apart: whether a poll is still
/// reachable, and whether its rows still exist.
/// </summary>
/// <remarks>
/// <see cref="LivePolls"/> is the access filter every read passes through, so a poll becomes
/// unreachable the moment its deadline passes (FR-039b) rather than when a job happens to run.
/// <see cref="EraseExpiredAsync"/> then removes the data, because Principle IV requires deletion
/// to remove responses rather than hide them (FR-039c).
/// </remarks>
public sealed class RetentionService(
    RundfrageDbContext db,
    BerlinClock clock,
    ILogger<RetentionService> logger)
{
    /// <summary>Polls that are still within their retention deadline. The only way in.</summary>
    public IQueryable<Poll> LivePolls() => db.Polls.Where(p => p.RetentionDeadline > clock.Now);

    /// <summary>
    /// Removes expired polls and everything beneath them. Safe to run repeatedly: it selects by
    /// deadline, so a second run finds nothing left to do (FR-039d).
    /// </summary>
    public async Task<int> EraseExpiredAsync(CancellationToken ct)
    {
        var removed = await db.Polls
            .Where(p => p.RetentionDeadline <= clock.Now)
            .ExecuteDeleteAsync(ct);

        if (removed > 0)
        {
            // FR-043a: the count, never a title or a token.
            logger.LogInformation("Retention removed {PollCount} expired polls", removed);
        }

        return removed;
    }
}

/// <summary>
/// Runs the erasure sweep. FR-039c requires it at least daily; hourly keeps the window between
/// "unreachable" and "gone" short without costing anything measurable.
/// </summary>
public sealed class RetentionSweep(IServiceProvider services, ILogger<RetentionSweep> logger)
    : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var retention = scope.ServiceProvider.GetRequiredService<RetentionService>();
                await retention.EraseExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // A failed sweep must not kill the host: the data stays unreachable either way
                // (FR-039b), and the next run will try again. Type only, never the message.
                logger.LogError("Retention sweep failed ({Detail}); will retry", ex.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
