using Rundfrage.Api.Data;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// Produces real backup files, through the same <see cref="BackupService"/> an operator downloads
/// from.
/// </summary>
/// <remarks>
/// Deliberately not a hand-built database. A restore test whose input was assembled by the test
/// proves that the restore can read what the test writes, which is not the question - the question
/// is whether it can read what this system produces. Feature 003 measured that a hand copy of the
/// storage file is silently short while the system runs, so "just copy rundfrage.db" is exactly
/// the wrong shortcut here as well (003 FR-003b).
/// </remarks>
public static class BackupFileFixture
{
    /// <summary>
    /// Takes a backup of the storage in <paramref name="dataDirectory"/> and returns a path the
    /// caller owns. The file is outside the data directory, so it cannot be mistaken for storage.
    /// </summary>
    public static async Task<string> CreateAsync(string dataDirectory, CancellationToken ct = default)
    {
        var service = new BackupService(new StorageDirectory(dataDirectory));
        var produced = await service.CreateAsync(ct);

        // BackupService hands over a temporary file the caller owns. Moving it to a name of our
        // own keeps the test's intent readable in a directory listing when something fails.
        var destination = Path.Combine(
            Path.GetTempPath(), $"rundfrage-test-backup-{Guid.NewGuid():n}.db");

        File.Move(produced, destination);
        return destination;
    }

    /// <summary>A file that is not a database at all, for the refusal cases (FR-019).</summary>
    public static string CreateNotABackup()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rundfrage-test-junk-{Guid.NewGuid():n}.db");
        File.WriteAllText(path, "This is not a SQLite database. It is a sentence.");
        return path;
    }

    /// <summary>A real database that is not one of ours - right file format, wrong contents.</summary>
    public static async Task<string> CreateForeignDatabaseAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(Path.GetTempPath(), $"rundfrage-test-foreign-{Guid.NewGuid():n}.db");

        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            $"Data Source={path};Pooling=False");
        await connection.OpenAsync(ct);
        StorageSetup.Apply(connection);

        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE Unrelated(Id INTEGER PRIMARY KEY); INSERT INTO Unrelated VALUES (1);";
        await command.ExecuteNonQueryAsync(ct);

        return path;
    }

    /// <summary>The upload form the restore endpoints expect.</summary>
    public static MultipartFormDataContent ToFormContent(string backupPath, bool confirm = true)
    {
        var content = new MultipartFormDataContent();

        var file = new ByteArrayContent(File.ReadAllBytes(backupPath));
        file.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(file, "file", Path.GetFileName(backupPath));

        if (confirm)
        {
            content.Add(new StringContent("true"), "confirm");
        }

        return content;
    }

    public static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A leftover in the temp directory must not fail a test that already passed.
        }
    }
}
