using Microsoft.EntityFrameworkCore;

namespace Rundfrage.Api.Data;

/// <summary>
/// Applies migrations at startup without ever preventing the host from serving (FR-024). A
/// conventional MigrateAsync() would throw when the storage cannot be opened and kill the host,
/// turning "storage unavailable" into "nothing responds".
/// </summary>
public static class DatabaseStartup
{
    /// <summary>
    /// Applies the schema, reporting failure rather than throwing it.
    /// </summary>
    /// <remarks>
    /// One attempt by default. The previous storage was a server in a second container that was
    /// commonly still accepting its first connections when the application came up, so retrying
    /// with a backoff was the difference between starting and not. A file has no such state: the
    /// directory is prepared moments earlier in the same process, and if it cannot be opened now
    /// it will not open four seconds later either. Retrying would only make every start against
    /// unusable storage take fifteen seconds - which is what it did, measured, before this was
    /// reconsidered.
    /// <para>
    /// The parameters remain so a test can ask for the retry behaviour explicitly rather than
    /// having to wait for it by default.
    /// </para>
    /// </remarks>
    /// <returns><c>true</c> if migrations were applied; <c>false</c> if they were abandoned.</returns>
    public static async Task<bool> ApplyMigrationsAsync(
        RundfrageDbContext db,
        ILogger logger,
        CancellationToken cancellationToken,
        int maxAttempts = 1,
        TimeSpan? baseDelay = null)
    {
        var delay = baseDelay ?? TimeSpan.FromSeconds(1);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Storage schema is up to date after {Attempts} attempt(s)", attempt);
                return true;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                // Type only - never the exception object or its message (002 FR-026).
                logger.LogWarning(
                    "Storage not ready on attempt {Attempt} of {MaxAttempts} ({Detail}); retrying",
                    attempt, maxAttempts, ex.GetType().Name);

                await Task.Delay(delay, cancellationToken);
                delay *= 2;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Storage schema could not be applied ({Detail}). The application will start "
                    + "and report storage as unavailable; the next start will apply the schema",
                    ex.GetType().Name);

                return false;
            }
        }

        return false;
    }
}
