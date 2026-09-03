using Rundfrage.Api.Data;
using Rundfrage.Api.Polls;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// FR-021a: a download names what it is and when it was taken, so several can share a folder
/// without overwriting each other.
/// </summary>
/// <remarks>
/// The awkward cases live here rather than in the integration tests, which would need a poll in
/// storage for each of them. A title may be 300 characters long (002 FR-015) and may contain
/// anything a person can type - including nothing that survives into a file name.
/// </remarks>
public class DownloadNameTests
{
    private static readonly DateTime Moment = new(2026, 9, 3, 10, 15, 0, DateTimeKind.Utc);

    [Fact]
    public void An_export_is_named_after_its_poll_and_the_moment()
    {
        Assert.Equal("grillabend-2026-09-03T101500Z.json", PollExport.FileNameFor("Grillabend", Moment));
    }

    [Theory]
    [InlineData("Grillabend im Juli", "grillabend-im-juli")]
    [InlineData("Team-Meeting: Q3 / Planung", "team-meeting-q3-planung")]
    [InlineData("   Führung   ", "führung")]
    [InlineData("Wann?!", "wann")]
    public void Punctuation_and_spacing_collapse_into_one_separator(string title, string expectedSlug)
    {
        Assert.Equal($"{expectedSlug}-2026-09-03T101500Z.json", PollExport.FileNameFor(title, Moment));
    }

    [Fact]
    public void A_title_that_leaves_nothing_still_produces_a_usable_name()
    {
        // "???" is a title someone will eventually use, and a file called "-2026-...json" is not.
        var name = PollExport.FileNameFor("???", Moment);

        Assert.Equal("umfrage-2026-09-03T101500Z.json", name);
    }

    [Fact]
    public void A_very_long_title_is_shortened_without_leaving_a_dangling_separator()
    {
        var name = PollExport.FileNameFor(new string('a', 300), Moment);

        Assert.Equal($"{new string('a', 60)}-2026-09-03T101500Z.json", name);
        Assert.DoesNotContain("--", name);
    }

    [Fact]
    public void Two_exports_of_one_poll_taken_a_second_apart_do_not_collide()
    {
        // The reason the moment is in the name at all.
        Assert.NotEqual(
            PollExport.FileNameFor("Grillabend", Moment),
            PollExport.FileNameFor("Grillabend", Moment.AddSeconds(1)));
    }

    [Fact]
    public void A_backup_is_named_after_the_system_and_the_moment()
    {
        Assert.Equal("rundfrage-2026-09-03T101500Z.db", BackupService.FileNameFor(Moment));
    }
}
