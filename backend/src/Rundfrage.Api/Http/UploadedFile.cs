using Microsoft.AspNetCore.Http.Features;

namespace Rundfrage.Api.Http;

/// <summary>
/// An uploaded file that does not outlive the request that received it (FR-005a).
/// </summary>
/// <remarks>
/// The deletion belongs to the type rather than to each call site, because the path that leaks is
/// never the one anybody writes deliberately: an import refused on its second check, or one that
/// throws halfway, leaves a file nobody returns to. Feature 003 met the same problem in the
/// download direction and solved it with <c>FileOptions.DeleteOnClose</c> for the same reason.
/// <para>
/// That flag cannot be used here. The whole point of receiving the upload is to open it again by
/// path - SQLite is handed a file name, not a handle - and a file opened with
/// <c>DeleteOnClose</c> is unlinked the moment the writing handle closes. So the file is written,
/// closed, and deleted on dispose instead, which is what makes it reopenable and still temporary.
/// </para>
/// <para>
/// It is created for this account only. A backup upload carries every participant link and every
/// personal link in the system, so while it exists it is as sensitive as the storage itself
/// (003 FR-007a).
/// </para>
/// </remarks>
public sealed class UploadedFile : IAsyncDisposable
{
    private bool _disposed;

    private UploadedFile(string path) => Path = path;

    /// <summary>Where the upload landed. Valid until this instance is disposed.</summary>
    public string Path { get; }

    /// <summary>
    /// Streams <paramref name="source"/> to a temporary file. Never buffers the whole upload in
    /// memory: no size limit applies (spec Assumptions, research R-5), so the largest thing anyone
    /// sends must not also be the largest thing the process holds.
    /// </summary>
    public static async Task<UploadedFile> ReceiveAsync(Stream source, CancellationToken ct)
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"rundfrage-upload-{Guid.NewGuid():n}");

        try
        {
            await using var destination = new FileStream(
                path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 64 * 1024, useAsync: true);

            await source.CopyToAsync(destination, ct);
        }
        catch
        {
            // A partial upload is still an upload. Removing it here keeps FR-005a true on the
            // path where the caller never receives an instance to dispose.
            TryDelete(path);
            throw;
        }

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (IOException)
            {
                // Tightening failed; the file still must not be kept. Deletion on dispose stands.
            }
        }

        return new UploadedFile(path);
    }

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;

            // The companions too, and this is not defensive tidying. Opening the upload as a
            // database - which is exactly what a restore does to verify it - leaves `-wal` and
            // `-shm` beside it, and those carry the same data as the file itself. Deleting only
            // the one that is easy to think of would leave every participant link and every
            // personal link on disk while reporting that nothing was kept.
            //
            // Feature 003 met the same asymmetry from the other side: StorageSetup.SecureFile
            // tightens permissions on all three for the same reason.
            foreach (var suffix in Companions)
            {
                TryDelete(Path + suffix);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>The file itself and the two the storage engine may create beside it.</summary>
    private static readonly string[] Companions = ["", "-wal", "-shm"];

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Whatever failure brought us here is the one the caller needs to hear about.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>
/// Receiving the one file an import request carries, without a size limit (research R-5).
/// </summary>
/// <remarks>
/// The specification records a deliberate decision that neither upload is size-limited, with the
/// accepted consequence written into its Assumptions. Left alone, ASP.NET Core imposes 28.6 MB
/// (Kestrel) and 128 MB (multipart) anyway — limits nobody chose, that contradict a decision that
/// <em>was</em> chosen, and that would surface as an opaque 413 on the day someone's backup grew.
/// Both are lifted here, at one place, for these routes only.
/// <para>
/// <b>This is also exactly where a limit would go back</b> if the accepted consequence ever stops
/// being acceptable. It is one value, and the specification's Assumptions section says what would
/// have to change first.
/// </para>
/// <para>
/// The form is read here rather than bound as an <c>IFormFile</c> parameter, because parameter
/// binding reads the body before anything in the handler runs — which is too late to lift the
/// limit that would already have rejected it.
/// </para>
/// </remarks>
public static class ImportUpload
{
    /// <summary>What the field is called in the multipart body.</summary>
    public const string FieldName = "file";

    public static async Task<UploadedFile?> ReceiveAsync(
        HttpContext context, CancellationToken ct)
    {
        var sizeLimit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeLimit is { IsReadOnly: false })
        {
            sizeLimit.MaxRequestBodySize = null;
        }

        // Per-request form options, so lifting the multipart limit here does not lift it for
        // every other route in the application.
        context.Features.Set<IFormFeature>(new FormFeature(context.Request, new FormOptions
        {
            MultipartBodyLengthLimit = long.MaxValue,
            ValueLengthLimit = int.MaxValue,
            MultipartHeadersLengthLimit = int.MaxValue,
        }));

        if (!context.Request.HasFormContentType)
        {
            return null;
        }

        var form = await context.Request.ReadFormAsync(ct);
        var file = form.Files[FieldName];

        if (file is null)
        {
            return null;
        }

        await using var stream = file.OpenReadStream();
        return await UploadedFile.ReceiveAsync(stream, ct);
    }

    /// <summary>Whether the operator confirmed a destructive operation (FR-018).</summary>
    public static bool Confirmed(HttpContext context) =>
        context.Request.HasFormContentType
        && context.Request.Form.TryGetValue("confirm", out var value)
        && bool.TryParse(value, out var confirmed)
        && confirmed;
}
