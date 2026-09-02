namespace Rundfrage.Api.Diagnostics;

/// <summary>
/// The backend has exactly two states. It cannot report its own absence - the UI's third
/// state, "backend unreachable", is derived client-side (research.md R-4, data-model.md §3).
/// </summary>
public enum DatabaseState
{
    Reachable,
    Unreachable,
}

/// <summary>
/// Outcome of one database reachability check. Produced on demand, never stored, never cached
/// (acceptance scenario 2.4). Carries no exception text, host name, or connection string,
/// because FR-014 forbids sending those to the browser - they are absent from the type rather
/// than filtered out later.
/// </summary>
public sealed record ConnectivityStatus(
    DatabaseState State,
    DateTimeOffset CheckedAt,
    int DurationMs);
