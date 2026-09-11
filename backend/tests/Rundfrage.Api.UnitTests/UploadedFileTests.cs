using System.Text;
using Rundfrage.Api.Http;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// FR-005a: an uploaded file must not outlive the import that reads it.
/// </summary>
/// <remarks>
/// The failing path is the one that matters and the one that is easy to miss. A backup upload is
/// every participant link and every personal link in the system; a copy left behind after a
/// refusal is a second, unguarded original of the whole thing. Feature 003 solved the same trap in
/// the download direction with <c>FileOptions.DeleteOnClose</c>, and it solved it precisely
/// because deleting after an awaited copy survives the abandoned case.
/// </remarks>
public class UploadedFileTests
{
    private static Stream Body(string content = "{}") =>
        new MemoryStream(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task Writes_the_upload_where_it_can_be_read_back()
    {
        await using var upload = await UploadedFile.ReceiveAsync(Body("hello"), CancellationToken.None);

        Assert.True(File.Exists(upload.Path));
        Assert.Equal("hello", await File.ReadAllTextAsync(upload.Path));
    }

    [Fact]
    public async Task Is_deleted_when_the_operation_succeeds()
    {
        string path;

        await using (var upload = await UploadedFile.ReceiveAsync(Body(), CancellationToken.None))
        {
            path = upload.Path;
            Assert.True(File.Exists(path));
        }

        Assert.False(File.Exists(path), "a completed import must leave no upload behind");
    }

    [Fact]
    public async Task Is_deleted_when_the_import_is_refused_without_reading_it()
    {
        // The refusal path: received, inspected, rejected. Nothing read it to the end.
        string path;

        await using (var upload = await UploadedFile.ReceiveAsync(Body(), CancellationToken.None))
        {
            path = upload.Path;
        }

        Assert.False(File.Exists(path), "a refused import must leave no upload behind");
    }

    [Fact]
    public async Task Is_deleted_when_the_caller_throws_partway()
    {
        // The path that accumulates leftovers unnoticed, because nothing reports it.
        string? path = null;

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var upload = await UploadedFile.ReceiveAsync(Body(), CancellationToken.None);
            path = upload.Path;
            throw new InvalidOperationException("the import failed halfway");
        });

        Assert.NotNull(path);
        Assert.False(File.Exists(path), "a failed import must leave no upload behind");
    }

    [Fact]
    public async Task Deletes_the_journal_companions_as_well()
    {
        // Regression. Opening the upload as a database - which verifying a backup does - leaves
        // `-wal` and `-shm` beside it, and they carry the same data as the file itself. Deleting
        // only the main file left every link in the system on disk while reporting that nothing
        // was kept; an integration test counting temporary files is what caught it.
        string path;

        await using (var upload = await UploadedFile.ReceiveAsync(Body(), CancellationToken.None))
        {
            path = upload.Path;
            await File.WriteAllTextAsync(path + "-wal", "journal");
            await File.WriteAllTextAsync(path + "-shm", "shared memory");
        }

        Assert.False(File.Exists(path), "the upload itself");
        Assert.False(File.Exists(path + "-wal"), "the write-ahead log carries the same data");
        Assert.False(File.Exists(path + "-shm"), "the shared-memory file carries the same data");
    }

    [Fact]
    public async Task Disposing_twice_is_not_an_error()
    {
        var upload = await UploadedFile.ReceiveAsync(Body(), CancellationToken.None);
        await upload.DisposeAsync();

        var exception = await Record.ExceptionAsync(async () => await upload.DisposeAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task Each_upload_gets_its_own_file()
    {
        await using var first = await UploadedFile.ReceiveAsync(Body("a"), CancellationToken.None);
        await using var second = await UploadedFile.ReceiveAsync(Body("b"), CancellationToken.None);

        Assert.NotEqual(first.Path, second.Path);
    }
}
