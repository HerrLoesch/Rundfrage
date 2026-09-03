using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>User Story 3: the grid and the per-day totals (FR-032 to FR-036c).</summary>
public class ResultsTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private sealed record Poll(string Token, Guid Id, Guid[] DayIds);

    private static async Task<Poll> CreatePollAsync(ApiFactory factory, params string[] days)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var created = await admin.PostAsJsonAsync(
            "/api/v1/admin/polls", new { title = "Auswertung", days });

        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        var token = summary.GetProperty("participantToken").GetString()!;
        var view = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{token}");

        return new Poll(
            token,
            summary.GetProperty("id").GetGuid(),
            [.. view.GetProperty("days").EnumerateArray().Select(d => d.GetProperty("id").GetGuid())]);
    }

    private static async Task AnswerAsync(
        ApiFactory factory, Poll poll, string name, params (Guid Day, string Availability)[] answers) =>
        await factory.CreateClient().PostAsJsonAsync($"/api/v1/polls/{poll.Token}/responses", new
        {
            displayName = name,
            answers = answers.Select(a => new { dayId = a.Day, availability = a.Availability }),
        });

    [Fact]
    public async Task Totals_count_only_the_three_answered_states()
    {
        // FR-033: they need not sum to the response count, because *no answer* is not counted.
        using var factory = new ApiFactory(postgres.ConnectionString);
        var poll = await CreatePollAsync(factory, "2026-11-18", "2026-11-20");

        await AnswerAsync(factory, poll, "Anna", (poll.DayIds[0], "yes"), (poll.DayIds[1], "no"));
        await AnswerAsync(factory, poll, "Bernd", (poll.DayIds[0], "yes"));
        await AnswerAsync(factory, poll, "Christa", (poll.DayIds[0], "maybe"));

        var view = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{poll.Token}");
        var totals = view.GetProperty("totals").EnumerateArray().ToArray();

        var firstDay = totals.Single(t => t.GetProperty("dayId").GetGuid() == poll.DayIds[0]);
        Assert.Equal(2, firstDay.GetProperty("yes").GetInt32());
        Assert.Equal(1, firstDay.GetProperty("maybe").GetInt32());
        Assert.Equal(0, firstDay.GetProperty("no").GetInt32());

        // Bernd and Christa left the second day unanswered - so its totals sum to one, not three.
        var secondDay = totals.Single(t => t.GetProperty("dayId").GetGuid() == poll.DayIds[1]);
        var sum = secondDay.GetProperty("yes").GetInt32()
                  + secondDay.GetProperty("maybe").GetInt32()
                  + secondDay.GetProperty("no").GetInt32();
        Assert.Equal(1, sum);
        Assert.Equal(3, view.GetProperty("responseCount").GetInt32());
    }

    [Fact]
    public async Task Every_response_appears_as_its_own_row()
    {
        // FR-033a: the row count is what makes the uncounted state legible.
        using var factory = new ApiFactory(postgres.ConnectionString);
        var poll = await CreatePollAsync(factory, "2026-11-18");

        await AnswerAsync(factory, poll, "Anna", (poll.DayIds[0], "yes"));
        await AnswerAsync(factory, poll, "Bernd", (poll.DayIds[0], "no"));

        var view = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{poll.Token}");
        var names = view.GetProperty("responses").EnumerateArray()
            .Select(r => r.GetProperty("displayName").GetString() ?? string.Empty).ToArray();

        Assert.Equal(["Anna", "Bernd"], names);
    }

    [Fact]
    public async Task A_row_never_carries_an_edit_token()
    {
        // FR-029: the revision capability belongs to its holder alone.
        using var factory = new ApiFactory(postgres.ConnectionString);
        var poll = await CreatePollAsync(factory, "2026-11-18");

        var submitted = await factory.CreateClient().PostAsJsonAsync(
            $"/api/v1/polls/{poll.Token}/responses",
            new { displayName = "Anna", answers = new[] { new { dayId = poll.DayIds[0], availability = "yes" } } });

        var editToken = (await submitted.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("editToken").GetString()!;

        var body = await factory.CreateClient().GetStringAsync($"/api/v1/polls/{poll.Token}");

        Assert.DoesNotContain(editToken, body);
    }

    [Fact]
    public async Task A_poll_without_responses_returns_an_explicit_empty_state()
    {
        // FR-034: an empty grid, not an error and not a blank.
        using var factory = new ApiFactory(postgres.ConnectionString);
        var poll = await CreatePollAsync(factory, "2026-11-18");

        var view = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{poll.Token}");

        Assert.Equal(0, view.GetProperty("responseCount").GetInt32());
        Assert.Empty(view.GetProperty("responses").EnumerateArray());
        Assert.Equal(1, view.GetProperty("pageCount").GetInt32());
        Assert.NotEmpty(view.GetProperty("totals").EnumerateArray());
    }

    [Fact]
    public async Task Rows_are_paged_at_fifty()
    {
        // FR-036c and research.md R-7: 1000 x 100 must stay usable, so rows are paged.
        using var factory = new ApiFactory(postgres.ConnectionString);
        var poll = await CreatePollAsync(factory, "2026-11-18");

        for (var i = 0; i < 8; i++)
        {
            await AnswerAsync(factory, poll, $"Person {i}", (poll.DayIds[0], "yes"));
        }

        var view = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{poll.Token}");

        Assert.True(view.GetProperty("responses").GetArrayLength() <= 50);
        Assert.Equal(1, view.GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task The_operator_sees_the_same_grid_through_the_admin_route()
    {
        // FR-036: holding the link grants the grid; holding a session grants the admin area.
        using var factory = new ApiFactory(postgres.ConnectionString);
        var poll = await CreatePollAsync(factory, "2026-11-18");
        await AnswerAsync(factory, poll, "Anna", (poll.DayIds[0], "yes"));

        var admin = await factory.CreateSignedInClientAsync();
        var adminView = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/polls/{poll.Id}");
        var publicView = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{poll.Token}");

        Assert.Equal(publicView.GetProperty("responseCount").GetInt32(),
                     adminView.GetProperty("responseCount").GetInt32());
        Assert.Equal(publicView.GetProperty("title").GetString(),
                     adminView.GetProperty("title").GetString());
    }

    [Fact]
    public async Task An_unknown_poll_id_gives_the_operator_the_same_neutral_not_found()
    {
        using var factory = new ApiFactory(postgres.ConnectionString);
        var admin = await factory.CreateSignedInClientAsync();

        var response = await admin.GetAsync("/api/v1/admin/polls/0199a000-0000-7000-8000-000000000000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("{\"code\":\"not_found\"}", await response.Content.ReadAsStringAsync());
    }
}
