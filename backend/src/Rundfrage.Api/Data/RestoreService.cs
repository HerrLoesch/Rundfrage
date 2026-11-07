using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Http;
using Rundfrage.Api.Retention;
using Rundfrage.Api.Time;

namespace Rundfrage.Api.Data;

/// <summary>What a backup holds, and what restoring it would cost (FR-018).</summary>
/// <remarks>
/// The wish-list counts were added by feature 008, and not as an extension: without them this
/// record would have understated what a restore destroys. 005 FR-018 makes the operator confirm
/// against a statement of the loss, and once wish lists exist a statement that counts only polls
/// is wrong rather than incomplete - it would say "you lose 0 polls" while twelve wish lists went
/// with them (008 research R-10).
/// <para>
/// The Ersteller counts were added by feature 009 for exactly the same reason, one feature later
/// (009 FR-052). A restore replaces every link that exists with every link the backup held, so an
/// operator who restores a file taken before they issued ten Ersteller links has just invalidated
/// all ten - and would have been told nothing about it.
/// </para>
/// <para>
/// The form and response counts were added by feature 010 for the same reason again (010 FR-047):
/// without them the preview understates what a restore destroys once forms exist, the exact defect
/// 008's research R-10 recorded for wish lists.
/// </para>
/// </remarks>
public sealed record RestorePreview(
    int PollsInBackup,
    int ResponsesInBackup,
    int PollsLost,
    int ResponsesLost,
    int WishListsInBackup,
    int ClaimsInBackup,
    int WishListsLost,
    int ClaimsLost,
    int CreatorsInBackup,
    int CreatorsLost,
    int FormsInBackup,
    int FormResponsesInBackup,
    int FormsLost,
    int FormResponsesLost,
    IReadOnlyList<string> Expired);

/// <summary>
/// What a restore did. Its existence is the success: a refusal returns a
/// <see cref="RestoreRefusal"/> instead, so there is no "restored nothing" state to report.
/// </summary>
/// <remarks>
/// This deliberately has no <c>Restored</c> flag, unlike <see cref="Rundfrage.Api.Polls.ImportSummary"/>,
/// whose <c>Imported</c> can genuinely be false - a file holding one expired poll takes nothing and
/// that is a specified outcome (FR-004). A restore has no equivalent: it either replaced everything
/// or it was refused. A field that could only ever be <c>true</c> would invite an assertion that
/// tests a constant.
/// </remarks>
public sealed record RestoreSummary(int Polls, int Responses, IReadOnlyList<string> Expired);

/// <summary>Why a restore was refused. Nothing was replaced.</summary>
public sealed record RestoreRefusal(string Code);

/// <summary>
/// Replaces all data with the contents of a backup (FR-016 to FR-024).
/// </summary>
/// <remarks>
/// <b>The mirror of <see cref="BackupService"/>.</b> That class copies the live database out
/// through SQLite's online backup mechanism; this one copies a file in through the same mechanism,
/// with the live database as the destination. Measured (research R-1): a connection that was
/// already open sees the restored data immediately, and the tokens come with it — which is what
/// makes FR-017 achievable without any file being moved and without anything being reissued.
/// <para>
/// The order of operations is load-bearing and is set out in data-model.md §5. Steps 1 to 4 cannot
/// lose anything, because the live storage is not written until step 6; by then the upload has
/// been proven readable and the retention sweep is already held off. Step 5 is what makes a
/// failure in 6 or 7 recoverable.
/// </para>
/// </remarks>
public sealed class RestoreService(
    StorageDirectory storage,
    RetentionSuspension suspension,
    BerlinClock clock,
    IServiceScopeFactory scopes,
    ILogger<RestoreService> logger)
{
    /// <summary>
    /// SQLITE_BUSY. The engine's own code for "another connection holds this database", which is
    /// what research R-2 measured a restore running into.
    /// </summary>
    private const int SqliteBusy = 5;

    /// <summary>
    /// Whether the file is a backup this system can restore, checked without touching anything
    /// live (FR-019, research R-4).
    /// </summary>
    public async Task<bool> VerifyAsync(string path, CancellationToken ct)
    {
        try
        {
            await using var candidate = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
            await candidate.OpenAsync(ct);

            await using var integrity = candidate.CreateCommand();
            integrity.CommandText = "PRAGMA integrity_check";

            if (await integrity.ExecuteScalarAsync(ct) as string != "ok")
            {
                return false;
            }

            // Structurally sound is not enough: a valid database of somebody else's would pass
            // integrity_check and then replace every poll in this one with nothing.
            await using var shape = candidate.CreateCommand();
            shape.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Polls', 'Responses')";

            return Convert.ToInt64(await shape.ExecuteScalarAsync(ct)) == 2;
        }
        catch (SqliteException)
        {
            // "file is not a database" arrives here, which is the common case and not an error
            // worth a stack trace.
            return false;
        }
    }

    public async Task<(RestoreRefusal? Refusal, RestorePreview? Preview)> PreviewAsync(
        string path, CancellationToken ct)
    {
        if (!await VerifyAsync(path, ct))
        {
            return (new RestoreRefusal(ErrorCodes.NotABackup), null);
        }

        var backup = await ReadCountsAsync(path, ct);
        var current = await ReadCountsAsync(StorageLocation.FileIn(storage.Path), ct);

        // What the operator is told they will lose. Never negative: a backup larger than the
        // current state loses nothing, and "-3 polls will be lost" would be nonsense.
        return (null, new RestorePreview(
            backup.Polls,
            backup.Responses,
            Math.Max(0, current.Polls - backup.Polls),
            Math.Max(0, current.Responses - backup.Responses),
            backup.WishLists,
            backup.Claims,
            Math.Max(0, current.WishLists - backup.WishLists),
            Math.Max(0, current.Claims - backup.Claims),
            backup.Creators,
            Math.Max(0, current.Creators - backup.Creators),
            backup.Forms,
            backup.FormResponses,
            Math.Max(0, current.Forms - backup.Forms),
            Math.Max(0, current.FormResponses - backup.FormResponses),
            backup.Expired));
    }

    public async Task<(RestoreRefusal? Refusal, RestoreSummary? Summary)> RestoreAsync(
        string path, CancellationToken ct)
    {
        // Step 3 (steps 1 and 2 belong to the endpoint): prove the upload readable before
        // anything live is touched.
        if (!await VerifyAsync(path, ct))
        {
            return (new RestoreRefusal(ErrorCodes.NotABackup), null);
        }

        // Step 4: hold the retention sweep off. Without this a sweep waking mid-restore holds a
        // transaction and the restore fails with 'database is locked' (research R-2).
        using var held = await suspension.AcquireAsync(ct);

        // Step 5: the previous data, kept until the restore has succeeded (FR-021).
        var safetyCopy = await TakeSafetyCopyAsync(ct);

        try
        {
            // Step 6: the upload becomes the live database, tokens and all (FR-017, FR-020).
            await ReplaceStorageAsync(path, ct);

            // Step 7: the backup carried its own schema, including its migration history, so an
            // older one leaves the application reading a shape it was not built for (research R-3).
            await using (var scope = scopes.CreateAsyncScope())
            {
                await DatabaseStartup.ApplyMigrationsAsync(
                    scope.ServiceProvider.GetRequiredService<RundfrageDbContext>(), logger, ct);
            }

            StorageSetup.SecureFile(storage.Path, logger);

            var restored = await ReadCountsAsync(StorageLocation.FileIn(storage.Path), ct);

            logger.LogInformation(
                "Storage restored from a backup: {PollCount} polls, {ResponseCount} responses, "
                + "{WishListCount} wish lists, {ExpiredCount} of the polls already past their "
                + "retention date",
                restored.Polls, restored.Responses, restored.WishLists, restored.Expired.Count);

            return (null, new RestoreSummary(restored.Polls, restored.Responses, restored.Expired));
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteBusy)
        {
            // The measured failure (research R-2). A refusal the operator can act on, not a 500:
            // nothing was replaced, and trying again in a moment is the right advice.
            logger.LogWarning("A restore could not take the storage exclusively; nothing was replaced");
            await RollBackAsync(safetyCopy, ct);

            return (new RestoreRefusal(ErrorCodes.StorageLocked), null);
        }
        catch (Exception ex)
        {
            // FR-021: a failed restore must not cost the operator what they already had.
            logger.LogError(
                "A restore failed ({Detail}); the previous data is being put back", ex.GetType().Name);
            await RollBackAsync(safetyCopy, ct);

            throw;
        }
        finally
        {
            TryDelete(safetyCopy);
        }
    }

    /// <summary>A copy of the current storage, so step 6 is reversible.</summary>
    private async Task<string?> TakeSafetyCopyAsync(CancellationToken ct)
    {
        if (!File.Exists(StorageLocation.FileIn(storage.Path)))
        {
            return null;
        }

        try
        {
            return await new BackupService(storage).CreateAsync(ct);
        }
        catch (Exception ex)
        {
            // Reported rather than thrown: a system whose storage cannot be copied is one whose
            // storage is already in trouble, and refusing the restore would leave it there.
            logger.LogWarning(
                "No safety copy could be taken before the restore ({Detail})", ex.GetType().Name);
            return null;
        }
    }

    private async Task RollBackAsync(string? safetyCopy, CancellationToken ct)
    {
        if (safetyCopy is null || !File.Exists(safetyCopy))
        {
            return;
        }

        try
        {
            await ReplaceStorageAsync(safetyCopy, ct);
            logger.LogInformation("The previous data was put back after a failed restore");
        }
        catch (Exception ex)
        {
            logger.LogError(
                "The previous data could not be put back ({Detail}). The safety copy is at the "
                + "system's temporary directory", ex.GetType().Name);
        }
    }

    /// <summary>
    /// Copies <paramref name="sourcePath"/> over the live database, in place.
    /// </summary>
    /// <remarks>
    /// No file is moved, renamed or deleted. Doing it through the engine rather than the file
    /// system is what makes it safe while connections are open: it takes the same locks as
    /// everything else, so the storage is never briefly absent and a crash halfway leaves a
    /// database rather than a gap.
    /// </remarks>
    private async Task ReplaceStorageAsync(string sourcePath, CancellationToken ct)
    {
        await using var source = new SqliteConnection($"Data Source={sourcePath};Mode=ReadOnly;Pooling=False");
        await source.OpenAsync(ct);

        await using var destination = new SqliteConnection(
            StorageLocation.ConnectionStringFor(storage.Path));
        await destination.OpenAsync(ct);

        // The same settings the rest of the application runs under. The busy timeout matters here
        // for the same reason it does in BackupService.
        StorageSetup.Apply(destination);

        source.BackupDatabase(destination);
    }

    /// <summary>
    /// Instance rather than static, so that "now" comes from <see cref="BerlinClock"/> like every
    /// other instant in this system. A local <c>DateTime.UtcNow</c> here would be a second
    /// authority on time, and the first thing to disagree with the retention sweep it is meant to
    /// predict.
    /// </summary>
    private async Task<Counts> ReadCountsAsync(string databasePath, CancellationToken ct)
    {
        if (!File.Exists(databasePath))
        {
            return Counts.None;
        }

        try
        {
            await using var connection = new SqliteConnection(
                $"Data Source={databasePath};Mode=ReadOnly;Pooling=False");
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT (SELECT COUNT(*) FROM Polls), (SELECT COUNT(*) FROM Responses)";

            int polls, responses;
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct))
                {
                    return Counts.None;
                }

                polls = reader.GetInt32(0);
                responses = reader.GetInt32(1);
            }

            // Asked separately, and tolerantly: a backup taken before feature 008 has no wish-list
            // tables at all, and such a file is still a perfectly restorable backup. Counting zero
            // is the truth about it - failing to read it would refuse a valid restore.
            var (wishLists, claims) = await ReadWishCountsAsync(connection, ct);
            var creators = await ReadCreatorCountAsync(connection, ct);
            var (forms, formResponses) = await ReadFormCountsAsync(connection, ct);

            // FR-016a: restored as they are, and named so the operator is not surprised when the
            // next sweep removes them.
            var expired = new List<string>();
            await using var expiredCommand = connection.CreateCommand();
            expiredCommand.CommandText =
                "SELECT Title FROM Polls WHERE RetentionDeadline <= $now ORDER BY Title";
            expiredCommand.Parameters.AddWithValue("$now", clock.Now.ToString("O"));

            await using (var reader = await expiredCommand.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    expired.Add(reader.GetString(0));
                }
            }

            return new Counts(polls, responses, wishLists, claims, creators, forms, formResponses, expired);
        }
        catch (SqliteException)
        {
            return Counts.None;
        }
    }

    /// <summary>
    /// The wish-list counts, or zero where the tables are absent.
    /// </summary>
    /// <remarks>
    /// A backup taken before feature 008 has no such tables. That file is still a valid backup -
    /// <see cref="VerifyAsync"/> deliberately asks only for Polls and Responses - so the absence
    /// is answered with zero rather than with a refusal.
    /// </remarks>
    private static async Task<(int WishLists, int Claims)> ReadWishCountsAsync(
        SqliteConnection connection, CancellationToken ct)
    {
        await using var present = connection.CreateCommand();
        present.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' "
            + "AND name IN ('WishLists', 'WishClaims')";

        if (Convert.ToInt64(await present.ExecuteScalarAsync(ct)) != 2)
        {
            return (0, 0);
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT (SELECT COUNT(*) FROM WishLists), (SELECT COUNT(*) FROM WishClaims)";

        await using var reader = await command.ExecuteReaderAsync(ct);

        return await reader.ReadAsync(ct)
            ? (reader.GetInt32(0), reader.GetInt32(1))
            : (0, 0);
    }

    /// <summary>
    /// The Ersteller count, or zero where the table is absent.
    /// </summary>
    /// <remarks>
    /// Asked separately and tolerantly, exactly as the wish-list counts are: a backup taken before
    /// feature 009 has no <c>Creators</c> table and is still a perfectly restorable backup.
    /// Counting zero is the truth about such a file; failing to read it would refuse a valid
    /// restore (009 data-model section 9).
    /// </remarks>
    private static async Task<int> ReadCreatorCountAsync(
        SqliteConnection connection, CancellationToken ct)
    {
        await using var present = connection.CreateCommand();
        present.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Creators'";

        if (Convert.ToInt64(await present.ExecuteScalarAsync(ct)) != 1)
        {
            return 0;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Creators";

        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    /// <summary>
    /// The form count, or zero where the table is absent (010 data-model.md §... mirrors 009's
    /// tolerant read for Creators, one feature later).
    /// </summary>
    private static async Task<(int Forms, int FormResponses)> ReadFormCountsAsync(
        SqliteConnection connection, CancellationToken ct)
    {
        await using var present = connection.CreateCommand();
        present.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' "
            + "AND name IN ('Forms', 'FormResponses')";

        if (Convert.ToInt64(await present.ExecuteScalarAsync(ct)) != 2)
        {
            return (0, 0);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT (SELECT COUNT(*) FROM Forms), (SELECT COUNT(*) FROM FormResponses)";

        await using var reader = await command.ExecuteReaderAsync(ct);

        return await reader.ReadAsync(ct)
            ? (reader.GetInt32(0), reader.GetInt32(1))
            : (0, 0);
    }

    /// <summary>What one database file holds, as the preview and the summary report it.</summary>
    private sealed record Counts(
        int Polls, int Responses, int WishLists, int Claims, int Creators,
        int Forms, int FormResponses,
        IReadOnlyList<string> Expired)
    {
        public static readonly Counts None = new(0, 0, 0, 0, 0, 0, 0, []);
    }

    private static void TryDelete(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }
}
