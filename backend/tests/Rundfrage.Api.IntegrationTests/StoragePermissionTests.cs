using Rundfrage.Api.Data;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// FR-007a, SC-011a: the storage is readable only by the account the application runs as.
/// </summary>
/// <remarks>
/// This is a modest guarantee and deliberately stated as one. Whoever can read the file can read
/// every poll and every answer without a password (FR-007b), and file permissions are the whole
/// of what the application can do about that - the rest is the host's business, which is also why
/// encryption at rest was rejected rather than deferred (FR-007c).
/// </remarks>
public class StoragePermissionTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    [Fact]
    public async Task Neither_the_storage_file_nor_its_companions_are_readable_by_anyone_else()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // The container runs Linux; there is nothing here to assert on Windows.
        }

        var directory = Path.Combine(storage.DataDirectory, "permissions");
        Directory.CreateDirectory(directory);

        using var factory = new ApiFactory(directory);
        using var client = factory.CreateClient();
        await client.GetAsync("/");

        var forbidden =
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

        // The companions carry the same answers as the main file, so securing only the file
        // whose name one thinks of would secure nothing.
        var checkedAtLeastOne = false;

        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = StorageLocation.FileIn(directory) + suffix;

            if (!File.Exists(path))
            {
                continue;
            }

            checkedAtLeastOne = true;
            var mode = File.GetUnixFileMode(path);

            Assert.True(
                (mode & forbidden) == 0,
                $"{Path.GetFileName(path)} is {mode}; it must be reachable by this account only (FR-007a)");
        }

        Assert.True(checkedAtLeastOne, "no storage file was found to check");
    }

    [Fact]
    public async Task The_data_directory_itself_is_not_open_to_others()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // A directory others may enter is a directory whose contents others may open by name,
        // and the file names here are not a secret.
        var directory = Path.Combine(storage.DataDirectory, "directory-mode");

        using var factory = new ApiFactory(directory);
        using var client = factory.CreateClient();
        await client.GetAsync("/");

        var mode = File.GetUnixFileMode(directory);

        Assert.True(
            (mode & (UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0,
            $"the data directory is {mode}; it must be enterable by this account only (FR-007a)");
    }
}
