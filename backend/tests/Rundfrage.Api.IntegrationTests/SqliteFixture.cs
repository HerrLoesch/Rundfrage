namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// A genuinely empty storage directory per test class. Replaces the disposable PostgreSQL
/// container: with a file there is nothing to start, so the suite no longer needs a Docker
/// daemon at all (research.md R-6).
/// </summary>
/// <remarks>
/// Per class rather than per test, matching what the container fixture provided - several
/// tests in a class deliberately share state. Reusing one directory for the whole suite would
/// not do: it is not empty after the first class, so <see cref="SchemaCreationTests"/> would
/// pass for the wrong reason.
/// </remarks>
public sealed class SqliteFixture : IAsyncLifetime
{
    /// <summary>The directory handed to the application as its data directory.</summary>
    public string DataDirectory { get; } = Path.Combine(
        Path.GetTempPath(), "rundfrage-tests", Guid.NewGuid().ToString("n"));

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(DataDirectory);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // The journal and shared-memory companions are deleted with the directory. Failing to
        // clean up must not fail a test that already passed, so the attempt is best-effort.
        try
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
        catch (IOException)
        {
        }

        return Task.CompletedTask;
    }
}
