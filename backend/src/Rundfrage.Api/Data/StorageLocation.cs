namespace Rundfrage.Api.Data;

/// <summary>
/// The single authority on where persistent state lives (FR-002, FR-007).
/// </summary>
/// <remarks>
/// One directory, one file, one name for the setting. The previous storage needed a host, a
/// port, a database name and a credential; a file needs a path, and putting that path together
/// in more than one place is how a test ends up asserting against a different file than the one
/// the application writes to.
/// </remarks>
public static class StorageLocation
{
    /// <summary>The configured directory. Compose sets it; tests set it; both use this name.</summary>
    public const string DataDirectoryVariable = "DATA_DIR";

    /// <summary>
    /// Relative on purpose. In the container Compose sets <c>DATA_DIR=/data</c> onto the mounted
    /// volume; on a developer's machine an absolute default like <c>/data</c> would need root to
    /// create, so the fallback is a directory beside the application (FR-007).
    /// </summary>
    public const string DefaultDataDirectory = "data";

    public const string FileName = "rundfrage.db";

    public static string DirectoryFrom(IConfiguration configuration) =>
        configuration[DataDirectoryVariable] is { Length: > 0 } configured
            ? configured
            : DefaultDataDirectory;

    public static string FileIn(string dataDirectory) => Path.Combine(dataDirectory, FileName);

    /// <summary>
    /// The maintenance marker, beside the storage rather than inside it (005 FR-030, research R-6).
    /// </summary>
    /// <remarks>
    /// It lives here for the same reason everything else about the directory layout does: a second
    /// place that builds this path is a second place that can disagree with the first. It is
    /// deliberately NOT the storage file and not one of its companions - a restore replaces those
    /// and must leave this one alone.
    /// </remarks>
    public const string MaintenanceMarkerName = "maintenance";

    public static string MaintenanceMarkerIn(string dataDirectory) =>
        Path.Combine(dataDirectory, MaintenanceMarkerName);

    /// <summary>
    /// The connection string for a directory. <c>Pooling=False</c> is deliberate: a pooled
    /// connection outlives the operation that opened it and keeps the file handle - which makes
    /// deleting a test's storage directory fail, and makes a backup's view of "the current file"
    /// harder to reason about than it needs to be.
    /// </summary>
    public static string ConnectionStringFor(string dataDirectory) =>
        $"Data Source={FileIn(dataDirectory)};Pooling=False";
}

/// <summary>
/// The resolved data directory, injectable. A record rather than a bare string so nothing can be
/// handed the wrong string by accident.
/// </summary>
public sealed record StorageDirectory(string Path);
