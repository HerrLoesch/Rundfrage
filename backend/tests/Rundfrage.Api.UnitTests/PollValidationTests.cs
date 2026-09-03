using Rundfrage.Api.Polls;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// FR-008, FR-010, FR-015. Pure validation, so the limits are testable without a database -
/// and so SC-017's "enforced server-side" has one place to be true.
/// </summary>
public class PollValidationTests
{
    private static readonly DateOnly[] OneDay = [new(2026, 10, 15)];

    [Fact]
    public void Accepts_a_minimal_poll()
    {
        Assert.Null(PollService.Validate("Grillabend", null, OneDay));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Refuses_a_missing_title(string? title)
    {
        var error = PollService.Validate(title, null, OneDay);

        Assert.NotNull(error);
        Assert.Equal("title_required", error.Code);
    }

    [Fact]
    public void Refuses_a_title_over_three_hundred_characters_and_names_the_limit()
    {
        var error = PollService.Validate(new string('a', 301), null, OneDay);

        Assert.NotNull(error);
        Assert.Equal("title_too_long", error.Code);
        Assert.Equal(300, error.Limit);
    }

    [Fact]
    public void Accepts_a_title_of_exactly_three_hundred_characters()
    {
        Assert.Null(PollService.Validate(new string('a', 300), null, OneDay));
    }

    [Fact]
    public void Refuses_a_message_over_two_thousand_characters()
    {
        var error = PollService.Validate("Titel", new string('a', 2001), OneDay);

        Assert.NotNull(error);
        Assert.Equal("message_too_long", error.Code);
        Assert.Equal(2000, error.Limit);
    }

    [Fact]
    public void Accepts_a_poll_without_a_message()
    {
        // FR-009: its absence must not prevent creation.
        Assert.Null(PollService.Validate("Titel", null, OneDay));
        Assert.Null(PollService.Validate("Titel", "", OneDay));
    }

    [Fact]
    public void Refuses_a_poll_with_no_day()
    {
        var error = PollService.Validate("Titel", null, []);

        Assert.NotNull(error);
        Assert.Equal("days_required", error.Code);
    }

    [Fact]
    public void Refuses_more_than_one_hundred_days_and_names_the_limit()
    {
        var days = Enumerable.Range(0, 101).Select(i => new DateOnly(2026, 1, 1).AddDays(i)).ToArray();

        var error = PollService.Validate("Titel", null, days);

        Assert.NotNull(error);
        Assert.Equal("too_many_days", error.Code);
        Assert.Equal(100, error.Limit);
    }

    [Fact]
    public void Counts_duplicates_once_against_the_day_limit()
    {
        // FR-012: a day selected twice is one day, so 101 selections of 50 distinct days is fine.
        var days = Enumerable.Range(0, 50)
            .SelectMany(i => new[] { new DateOnly(2026, 1, 1).AddDays(i), new DateOnly(2026, 1, 1).AddDays(i) })
            .ToArray();

        Assert.Null(PollService.Validate("Titel", null, days));
    }

    [Fact]
    public void Accepts_days_in_the_past()
    {
        // FR-014: a poll may cover a period already under way.
        Assert.Null(PollService.Validate("Titel", null, [new DateOnly(2000, 1, 1)]));
    }

    [Fact]
    public void Normalises_days_to_a_sorted_distinct_sequence()
    {
        // FR-012 and FR-013 together: stored once, presented chronologically.
        var input = new[]
        {
            new DateOnly(2026, 10, 20),
            new DateOnly(2026, 10, 15),
            new DateOnly(2026, 10, 20),
            new DateOnly(2026, 10, 17),
        };

        var normalised = PollService.NormaliseDays(input);

        Assert.Equal(
            [new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 17), new DateOnly(2026, 10, 20)],
            normalised);
    }
}
