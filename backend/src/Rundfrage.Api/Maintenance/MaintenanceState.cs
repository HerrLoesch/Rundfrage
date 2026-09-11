using System.Globalization;
using Rundfrage.Api.Data;
using Rundfrage.Api.Time;

namespace Rundfrage.Api.Maintenance;

/// <summary>
/// Whether the participant side is switched off for maintenance (FR-025, FR-029, FR-030).
/// </summary>
/// <remarks>
/// <b>A file beside the storage, not a row inside it.</b> Two requirements together rule out every
/// other place it could live (research R-6): FR-029 wants it to survive a restart, which rules out
/// memory; FR-030 wants it to survive a restore, which rules out the database, because a restore
/// replaces every row; and FR-033 wants it changeable without a restart, which rules out an
/// environment variable. A sibling file in the data directory is what is left, and R-1 confirms
/// rather than assumes that a restore touches only the database file and its companions.
/// <para>
/// <b>The file's presence is the state; its content is informational.</b> That asymmetry is
/// deliberate. A truncated or empty marker after a power cut must still read as "on" — the safe
/// direction — rather than throwing, or defaulting to "off" and quietly reopening the participant
/// side in the middle of a maintenance window.
/// </para>
/// <para>
/// Nothing is cached. FR-033 requires a change to take effect for the next request, and a cached
/// value would need invalidating from wherever the switch was thrown; one <c>File.Exists</c> per
/// request costs less than the bug that eventually escapes such an invalidation.
/// </para>
/// </remarks>
public sealed class MaintenanceState(
    StorageDirectory storage, BerlinClock clock, ILogger<MaintenanceState> logger)
{
    private string MarkerPath => StorageLocation.MaintenanceMarkerIn(storage.Path);

    /// <summary>True while the marker exists, whatever the marker says.</summary>
    public bool IsOn => File.Exists(MarkerPath);

    /// <summary>When it was switched on, if that can still be read. Never the state itself.</summary>
    public DateTime? Since
    {
        get
        {
            try
            {
                var content = File.ReadAllText(MarkerPath).Trim();

                return DateTime.TryParse(
                    content, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
                    ? parsed
                    : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Switches maintenance on. Already being on is not an error, and re-confirming does not move
    /// the moment — otherwise "seit &lt;Zeitpunkt&gt;" would creep forward on every click.
    /// </summary>
    public void TurnOn()
    {
        if (IsOn)
        {
            return;
        }

        try
        {
            File.WriteAllText(MarkerPath, clock.Now.ToString("O", CultureInfo.InvariantCulture));

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(MarkerPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            logger.LogInformation("Maintenance mode switched on; participant routes now answer with the notice");
        }
        catch (Exception ex)
        {
            // Type only, never the path (002 FR-026).
            logger.LogError("Maintenance mode could not be switched on ({Detail})", ex.GetType().Name);
            throw;
        }
    }

    /// <summary>Switches maintenance off. Already being off is not an error.</summary>
    public void TurnOff()
    {
        try
        {
            File.Delete(MarkerPath);
            logger.LogInformation("Maintenance mode switched off; participant routes are answering again");
        }
        catch (Exception ex)
        {
            logger.LogError("Maintenance mode could not be switched off ({Detail})", ex.GetType().Name);
            throw;
        }
    }
}
