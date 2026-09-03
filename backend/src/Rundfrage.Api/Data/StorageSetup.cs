using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Rundfrage.Api.Data;

/// <summary>
/// The storage settings that carry requirements, applied to every connection in one place.
/// </summary>
/// <remarks>
/// These are not tuning. Each one is a requirement made real, which is why they live here rather
/// than scattered through startup: that is how one of them ends up missing from the test
/// configuration and a guarantee quietly holds only in production.
/// <para>
/// Measured before writing this: on a bare connection the engine already gives
/// <c>synchronous=2</c> and <c>foreign_keys=1</c>, and the migration turns on write-ahead
/// logging. Three of the four therefore hold by accident today. That is exactly the reason to
/// state them - an inherited default is not a guarantee, and nothing announces it when it
/// changes.
/// </para>
/// </remarks>
public static class StorageSetup
{
    /// <summary>
    /// How long a contending writer waits before giving up (FR-009, FR-010).
    /// </summary>
    /// <remarks>
    /// The engine's default is 0: the second writer is refused immediately. With one writer at a
    /// time that would turn ordinary contention into failed submissions, and the response cap
    /// would appear to hold only because answers were being dropped.
    /// </remarks>
    public const int BusyTimeoutMilliseconds = 5_000;

    public static readonly IInterceptor Interceptor = new SettingsInterceptor();

    /// <summary>
    /// Creates the data directory if it is not there (FR-004), for this account only (FR-007a).
    /// </summary>
    /// <remarks>
    /// Never throws. FR-024 requires the application to start and serve even when its storage
    /// cannot be reached; a directory that cannot be created is one way for that to be true, and
    /// killing the host here would turn "storage unavailable" into "nothing responds".
    /// </remarks>
    public static void PrepareDirectory(string dataDirectory, ILogger logger)
    {
        try
        {
            Directory.CreateDirectory(dataDirectory);

            if (!OperatingSystem.IsWindows())
            {
                // Owner only. Whoever can read this directory can read every answer in it
                // (FR-007b), and this is the honest limit of what the application can do about
                // that - the rest is the host's business (FR-007c).
                File.SetUnixFileMode(
                    dataDirectory,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
        catch (Exception ex)
        {
            // Type only, never the path: it is not a credential, but it is not log material
            // either, and the rule against pasting storage detail into logs is easier to keep
            // than to re-argue at each call site (002 FR-026).
            logger.LogError(
                "The data directory could not be prepared ({Detail}). The application will start "
                + "and report storage as unavailable", ex.GetType().Name);
        }
    }

    /// <summary>
    /// Restricts the storage file to this account (FR-007a). Called after the schema is applied,
    /// because that is when the file first exists.
    /// </summary>
    public static void SecureFile(string dataDirectory, ILogger logger)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // The journal and shared-memory companions carry the same data as the main file, so
        // securing only the one that is easy to think of would secure nothing.
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = StorageLocation.FileIn(dataDirectory) + suffix;

            try
            {
                if (File.Exists(path))
                {
                    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Storage file permissions could not be tightened ({Detail})", ex.GetType().Name);
            }
        }
    }

    /// <summary>
    /// Applies the settings to an open connection.
    /// </summary>
    /// <remarks>
    /// Public because the interceptor is not the only door. A connection opened directly - the
    /// backup opens two - would otherwise get the engine's defaults, including a busy timeout of
    /// zero, and a backup taken while someone is answering would fail outright instead of
    /// waiting. That happened, intermittently, before this was shared.
    /// </remarks>
    public static void Apply(DbConnection connection)
    {
        using var command = connection.CreateCommand();

        // busy_timeout is interpolated rather than parameterised: a PRAGMA takes no parameters,
        // and the value is a constant in this assembly, not input.
        command.CommandText = $"""
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = FULL;
            PRAGMA foreign_keys = ON;
            PRAGMA busy_timeout = {BusyTimeoutMilliseconds};
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>Applies the settings to every connection the application opens through EF Core.</summary>
    private sealed class SettingsInterceptor : DbConnectionInterceptor
    {
        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
            => Apply(connection);

        public override Task ConnectionOpenedAsync(
            DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            Apply(connection);
            return Task.CompletedTask;
        }
    }
}
