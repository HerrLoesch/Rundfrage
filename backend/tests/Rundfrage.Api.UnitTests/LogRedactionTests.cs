using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// 002 FR-026: a storage failure is logged by exception type, and nothing else about the storage
/// reaches the log - not the path, not the exception object whose ToString() would carry it.
/// </summary>
/// <remarks>
/// This suite used to point at the connectivity probe, and its example of something that must not
/// leak was a password inside a connection string. Feature 003 removed the probe with the rest of
/// the walking skeleton, and there is no password in a connection string any more.
/// <para>
/// The requirement did not go away with them, so the suite moved to what still carries it: the
/// startup path, where a storage failure is caught and reported. What must not leak is now the
/// storage path - not because it is a credential, but because whoever knows it knows where every
/// answer lives (FR-007b), and a log is somewhere it has no reason to be.
/// </para>
/// </remarks>
public class LogRedactionTests
{
    private const string TellTale = "a-directory-name-that-must-not-be-logged";

    private static readonly string UnusableDirectory =
        Path.Combine(Path.GetTempPath(), "rundfrage-no-such-place", TellTale, "deeper");

    private static RundfrageDbContext ContextForUnusableStorage() =>
        new(new DbContextOptionsBuilder<RundfrageDbContext>()
            .UseSqlite(StorageLocation.ConnectionStringFor(UnusableDirectory))
            .Options);

    [Fact]
    public async Task A_storage_failure_names_the_exception_type_and_nothing_else()
    {
        await using var db = ContextForUnusableStorage();
        var logger = new RecordingLogger<RundfrageDbContext>();

        var applied = await DatabaseStartup.ApplyMigrationsAsync(db, logger, CancellationToken.None);

        Assert.False(applied);

        var logged = logger.AllText;
        Assert.DoesNotContain(TellTale, logged);
        Assert.DoesNotContain(UnusableDirectory, logged);
        Assert.Matches(@"[A-Za-z]+Exception", logged);
    }

    [Fact]
    public async Task The_raw_exception_is_never_attached_to_the_entry()
    {
        // Exception detail is where a path would realistically leak: the message is written
        // carefully, and then the exception object is handed to the logger beside it and its
        // ToString() carries everything the careful message left out.
        await using var db = ContextForUnusableStorage();
        var logger = new RecordingLogger<RundfrageDbContext>();

        await DatabaseStartup.ApplyMigrationsAsync(db, logger, CancellationToken.None);

        var entry = Assert.Single(logger.Entries);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public void Preparing_an_impossible_directory_is_reported_the_same_way()
    {
        // The other half of the startup path. It has its own catch, so it needs its own check -
        // a rule kept in one of two places is a rule that holds until someone uses the other.
        var blocker = Path.Combine(Path.GetTempPath(), $"rundfrage-blocker-{Guid.NewGuid():n}");
        File.WriteAllText(blocker, "not a directory");

        try
        {
            var logger = new RecordingLogger<RundfrageDbContext>();

            StorageSetup.PrepareDirectory(Path.Combine(blocker, TellTale), logger);

            Assert.DoesNotContain(TellTale, logger.AllText);
            Assert.Matches(@"[A-Za-z]+Exception", logger.AllText);
        }
        finally
        {
            File.Delete(blocker);
        }
    }
}
