namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// Asking whether the system left a temporary file behind, in a directory it does not own.
/// </summary>
/// <remarks>
/// Two requirements are checked by looking at the temporary directory — FR-021 (a download is never
/// kept) and 005 FR-005a (an upload never outlives its import) — and both were originally written as
/// a count taken before and a count taken after.
/// <para>
/// <b>That is not a question about this system.</b> The temporary directory is shared with every
/// other process on the machine, and in this suite specifically with the unit project, which runs at
/// the same time and exercises the same classes directly. Feature 005 made it worse by adding two
/// more producers of <c>rundfrage-backup-*</c>: the safety copy a restore takes, and the fixture
/// that builds backups for the restore tests. Measured afterwards: roughly one solution run in four
/// failed, on a different test each time, for a reason that had nothing to do with the behaviour
/// under test.
/// </para>
/// <para>
/// Comparing the <em>sets</em>, and only ever asking about files that appeared while the test ran,
/// makes the question attributable again.
/// </para>
/// </remarks>
public static class TempFiles
{
    public static HashSet<string> Snapshot(string pattern) =>
        [.. Directory.GetFiles(Path.GetTempPath(), pattern)];

    /// <summary>
    /// Files matching <paramref name="pattern"/> that appeared after <paramref name="before"/> was
    /// taken and are still there.
    /// </summary>
    /// <remarks>
    /// Settles before concluding, and the asymmetry is the whole point: a file the system failed to
    /// clean up is permanent and survives every attempt, while one belonging to a concurrent test
    /// lives for microseconds inside an <c>await using</c> and is gone by the next look. Without the
    /// settle the assertion would be right about the system and wrong about the clock.
    /// </remarks>
    public static async Task<IReadOnlyCollection<string>> SurvivorsSince(
        string pattern, HashSet<string> before)
    {
        var survivors = Array.Empty<string>();

        for (var attempt = 0; attempt < 20; attempt++)
        {
            survivors = [.. Snapshot(pattern).Except(before)];

            if (survivors.Length == 0)
            {
                return survivors;
            }

            await Task.Delay(50);
        }

        return survivors;
    }
}
