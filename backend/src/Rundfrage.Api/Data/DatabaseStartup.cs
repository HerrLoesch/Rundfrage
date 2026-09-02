using Microsoft.EntityFrameworkCore;

namespace Rundfrage.Api.Data;

/// <summary>
/// Applies migrations at startup (FR-013) without ever preventing the host from serving
/// (FR-011). A conventional MigrateAsync() would throw when the database is absent and kill
/// the host, turning "database down" into "nothing responds" - which would fail FR-011 and
/// SC-004. See research.md R-2.
/// </summary>
public static class DatabaseStartup
{
    /// <summary>
    /// Retries transient startup failures - PostgreSQL is commonly still accepting its first
    /// connections when the application comes up - and gives up quietly after the budget.
    /// </summary>
    /// <returns><c>true</c> if migrations were applied; <c>false</c> if they were abandoned.</returns>
    public static async Task<bool> ApplyMigrationsAsync(
        RundfrageDbContext db,
        ILogger logger,
        CancellationToken cancellationToken,
        int maxAttempts = 5,
        TimeSpan? baseDelay = null)
    {
        var delay = baseDelay ?? TimeSpan.FromSeconds(1);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database schema is up to date after {Attempts} attempt(s)", attempt);
                return true;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                // Type only - never the exception object or its message (FR-026).
                logger.LogWarning(
                    "Database not ready on attempt {Attempt} of {MaxAttempts} ({Detail}); retrying",
                    attempt, maxAttempts, ex.GetType().Name);

                await Task.Delay(delay, cancellationToken);
                delay *= 2;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Database schema could not be applied after {MaxAttempts} attempts ({Detail}). "
                    + "The application will start and report the database as unreachable; "
                    + "the next start will apply the schema",
                    maxAttempts, ex.GetType().Name);

                return false;
            }
        }

        return false;
    }
}
