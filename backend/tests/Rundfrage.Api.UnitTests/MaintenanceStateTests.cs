using Microsoft.Extensions.Logging.Abstractions;
using Rundfrage.Api.Data;
using Rundfrage.Api.Maintenance;
using Rundfrage.Api.Time;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// T032 and T033: the marker file that holds maintenance state (FR-025, research R-6).
/// </summary>
/// <remarks>
/// The state lives beside the storage rather than inside it, and these tests pin the reason:
/// FR-030 requires it to survive a restore, and a restore replaces every row. A flag in the
/// database would be switched off by restoring a backup taken before maintenance began - silently
/// reopening the participant side in the middle of the maintenance window.
/// </remarks>
public class MaintenanceStateTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "rundfrage-maintenance-tests", Guid.NewGuid().ToString("n"));

    public MaintenanceStateTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private MaintenanceState NewState() => new(
        new StorageDirectory(_directory),
        new BerlinClock(TimeProvider.System),
        NullLogger<MaintenanceState>.Instance);

    [Fact]
    public void Is_off_when_no_marker_is_present()
    {
        Assert.False(NewState().IsOn);
        Assert.Null(NewState().Since);
    }

    [Fact]
    public void Is_on_after_being_switched_on()
    {
        var state = NewState();
        state.TurnOn();

        Assert.True(state.IsOn);
        Assert.NotNull(state.Since);
    }

    [Fact]
    public void Is_off_again_after_being_switched_off()
    {
        var state = NewState();
        state.TurnOn();
        state.TurnOff();

        Assert.False(state.IsOn);
    }

    [Fact]
    public void Switching_on_twice_and_off_twice_are_not_errors()
    {
        var state = NewState();

        Assert.Null(Record.Exception(() => { state.TurnOn(); state.TurnOn(); }));
        Assert.True(state.IsOn);

        Assert.Null(Record.Exception(() => { state.TurnOff(); state.TurnOff(); }));
        Assert.False(state.IsOn);
    }

    [Fact]
    public void Survives_a_new_instance_reading_the_same_directory()
    {
        // FR-029 in miniature: nothing is held in memory, so a restarted process finds the state
        // the previous one left.
        NewState().TurnOn();

        Assert.True(NewState().IsOn);
    }

    [Fact]
    public void An_unreadable_marker_still_means_on()
    {
        // The safe direction. A truncated or empty file after a power cut must not read as "off"
        // and reopen the participant side; the file's presence is the state, its content is only
        // informational (data-model.md §1).
        var state = NewState();
        state.TurnOn();
        File.WriteAllText(Path.Combine(_directory, "maintenance"), string.Empty);

        Assert.True(NewState().IsOn);
        Assert.Null(NewState().Since);
    }

    [Fact]
    public void A_marker_with_nonsense_content_still_means_on()
    {
        NewState().TurnOn();
        File.WriteAllText(Path.Combine(_directory, "maintenance"), "not a timestamp at all");

        Assert.True(NewState().IsOn);
    }

    [Fact]
    public void Switching_on_again_does_not_move_the_since_moment()
    {
        // "seit <Zeitpunkt>" would otherwise creep forward every time the operator re-confirms.
        var state = NewState();
        state.TurnOn();
        var first = state.Since;

        Thread.Sleep(20);
        state.TurnOn();

        Assert.Equal(first, state.Since);
    }
}
