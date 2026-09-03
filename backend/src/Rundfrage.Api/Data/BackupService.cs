using Microsoft.Data.Sqlite;

namespace Rundfrage.Api.Data;

/// <summary>
/// Produces a consistent, self-contained copy of the storage while the system keeps running
/// (FR-003, FR-003a).
/// </summary>
/// <remarks>
/// This exists because copying the storage file is not a backup. Measured with writes in flight,
/// a plain copy of the main file produced a database with <em>no tables at all</em>: in this
/// journal mode the schema itself can still be sitting in the companion file. The copy looks
/// fine, weighs about the right amount, and fails on the day it is needed - which is why
/// FR-003b marks the hand-copy route unsupported and why this endpoint is the answer instead.
/// <para>
/// The engine's own online backup mechanism does the work. It reads a consistent snapshot under
/// the same locking as everything else, so a response is in the copy completely or not at all,
/// and it writes a destination that needs no companions.
/// </para>
/// </remarks>
public sealed class BackupService(StorageDirectory storage)
{
    /// <summary>
    /// Writes a backup to a temporary file and returns its path. The caller owns the file and is
    /// responsible for removing it - nothing is kept (FR-021).
    /// </summary>
    public async Task<string> CreateAsync(CancellationToken ct)
    {
        var destinationPath = Path.Combine(
            Path.GetTempPath(), $"rundfrage-backup-{Guid.NewGuid():n}.db");

        await using var source = new SqliteConnection(
            StorageLocation.ConnectionStringFor(storage.Path));
        await source.OpenAsync(ct);

        // The same settings the rest of the application runs under. The busy timeout is the one
        // that matters here: without it the copy is refused the moment it meets a writer, so a
        // backup would fail exactly when it is most worth taking - while people are answering.
        StorageSetup.Apply(source);

        await using var destination = new SqliteConnection($"Data Source={destinationPath};Pooling=False");
        await destination.OpenAsync(ct);

        source.BackupDatabase(destination);

        if (!OperatingSystem.IsWindows())
        {
            // The backup carries every answer the storage does, so it gets the same treatment
            // (FR-007a) for as long as it exists.
            File.SetUnixFileMode(destinationPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return destinationPath;
    }

    /// <summary>The name the download carries: the system and the moment (FR-021a).</summary>
    public static string FileNameFor(DateTime takenAtUtc) =>
        $"rundfrage-{takenAtUtc:yyyy-MM-dd'T'HHmmss'Z'}.db";
}
