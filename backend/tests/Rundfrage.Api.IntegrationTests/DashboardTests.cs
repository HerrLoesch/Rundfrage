using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Polls;
using Rundfrage.Api.Security;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 007 FR-028, FR-028a, FR-028b, FR-029, FR-034: the dashboard's figures, and what may not appear
/// among them.
/// </summary>
/// <remarks>
/// <b>Storage per test, not per class.</b> The other suites share one directory across a class
/// because several of their tests deliberately build on each other. These cannot: every figure
/// here counts the whole installation, so one test's polls become another's wrong answer - which
/// is exactly how the first run reported 42 responses where 3 were seeded. xUnit constructs the
/// test class once per test, so IAsyncLifetime here means one empty database each time.
/// </remarks>
public class DashboardTests : IAsyncLifetime
{
    private readonly SqliteFixture storage = new();

    public Task InitializeAsync() => storage.InitializeAsync();

    public Task DisposeAsync() => storage.DisposeAsync();

    private static async Task<JsonElement> DashboardAsync(HttpClient admin)
    {
        var response = await admin.GetAsync("/api/v1/admin/dashboard");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Creates a poll through the API and seeds answers directly beneath it.</summary>
    private static async Task<Guid> SeedPollAsync(
        ApiFactory factory,
        HttpClient admin,
        string title,
        int dayCount,
        params Availability[][] responses)
    {
        var days = Enumerable.Range(0, dayCount)
            .Select(i => new DateOnly(2027, 6, 1).AddDays(i).ToString("yyyy-MM-dd"))
            .ToArray();

        var created = await admin.PostAsJsonAsync("/api/v1/admin/polls", new { title, days });
        created.EnsureSuccessStatusCode();

        var pollId = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        if (responses.Length == 0)
        {
            return pollId;
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
        var dayIds = db.CandidateDays.Where(d => d.PollId == pollId).OrderBy(d => d.Date)
            .Select(d => d.Id).ToList();

        foreach (var answers in responses)
        {
            db.Responses.Add(new PollResponse
            {
                Id = Guid.CreateVersion7(),
                PollId = pollId,
                DisplayName = "Person",
                EditToken = CapabilityToken.Mint(),
                SubmittedAt = DateTime.UtcNow,
                // Only the days actually answered get a row - which is what makes the grouped
                // count in FR-028a correct without a filter.
                Answers = [.. answers.Select((a, i) => new DayAnswer
                {
                    CandidateDayId = dayIds[i],
                    Availability = a,
                })],
            });
        }

        await db.SaveChangesAsync();
        return pollId;
    }

    [Fact]
    public async Task An_installation_with_nothing_stored_reports_zeros_and_no_next_deletion()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var figures = await DashboardAsync(admin);

        Assert.Equal(0, figures.GetProperty("pollCount").GetInt32());
        Assert.Equal(0, figures.GetProperty("responseCount").GetInt32());
        Assert.Equal(0, figures.GetProperty("unansweredPolls").GetInt32());
        Assert.Equal(0, figures.GetProperty("deletionsDueSoon").GetInt32());

        // Null exactly when no poll exists. That is a different state from "nothing is due soon",
        // and the view is required not to collapse the two into one sentence.
        Assert.Equal(JsonValueKind.Null, figures.GetProperty("nextDeletion").ValueKind);
    }

    [Fact]
    public async Task The_figures_count_polls_responses_and_the_unanswered()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        // Two answered polls and one nobody has touched.
        await SeedPollAsync(factory, admin, "Beantwortet", 2,
            [Availability.Yes, Availability.No],
            [Availability.Yes, Availability.Maybe]);
        await SeedPollAsync(factory, admin, "Auch beantwortet", 1, [Availability.Yes]);
        await SeedPollAsync(factory, admin, "Unbeantwortet", 3);

        var figures = await DashboardAsync(admin);

        Assert.Equal(3, figures.GetProperty("pollCount").GetInt32());
        // Three responses, not the six day-answers beneath them: one person answering counts once.
        Assert.Equal(3, figures.GetProperty("responseCount").GetInt32());
        Assert.Equal(1, figures.GetProperty("unansweredPolls").GetInt32());
    }

    /// <summary>
    /// FR-028a and FR-029. The aggregate must equal the sum of every poll's per-day summary, and a
    /// poll with no answers must contribute nothing rather than zeros.
    /// </summary>
    [Fact]
    public async Task The_distribution_equals_the_sum_of_every_polls_per_day_summary()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        await SeedPollAsync(factory, admin, "Erste", 3,
            [Availability.Yes, Availability.Maybe, Availability.No],
            [Availability.Yes, Availability.Yes]);
        await SeedPollAsync(factory, admin, "Zweite", 2, [Availability.No, Availability.Maybe]);
        await SeedPollAsync(factory, admin, "Ohne Antworten", 2);

        var figures = await DashboardAsync(admin);

        // Summed independently, from the same projection the results grid uses. If these two ever
        // disagree, one of them is lying to the operator on the same screen as the other.
        var polls = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/polls");
        int yes = 0, maybe = 0, no = 0;

        foreach (var poll in polls.EnumerateArray())
        {
            var id = poll.GetProperty("id").GetGuid();
            var view = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/polls/{id}");

            foreach (var totals in view.GetProperty("totals").EnumerateArray())
            {
                yes += totals.GetProperty("yes").GetInt32();
                maybe += totals.GetProperty("maybe").GetInt32();
                no += totals.GetProperty("no").GetInt32();
            }
        }

        Assert.Equal(yes, figures.GetProperty("yes").GetInt32());
        Assert.Equal(maybe, figures.GetProperty("maybe").GetInt32());
        Assert.Equal(no, figures.GetProperty("no").GetInt32());

        // And the values themselves, so that a projection returning zeros for both could not pass.
        Assert.Equal(3, figures.GetProperty("yes").GetInt32());
        Assert.Equal(2, figures.GetProperty("maybe").GetInt32());
        Assert.Equal(2, figures.GetProperty("no").GetInt32());
    }

    [Fact]
    public async Task A_poll_past_its_deadline_is_counted_in_no_figure()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var expiring = await SeedPollAsync(factory, admin, "Abgelaufen", 1, [Availability.Yes]);
        await SeedPollAsync(factory, admin, "Lebt", 1, [Availability.No]);

        // Pushed past its deadline directly: a poll becomes unreachable when the deadline passes,
        // not when the sweep happens to run (002 FR-039b), so the rows are still on disk here.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
            var poll = db.Polls.Single(p => p.Id == expiring);
            poll.RetentionDeadline = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }

        var figures = await DashboardAsync(admin);

        Assert.Equal(1, figures.GetProperty("pollCount").GetInt32());
        Assert.Equal(1, figures.GetProperty("responseCount").GetInt32());
        Assert.Equal(0, figures.GetProperty("yes").GetInt32());
        Assert.Equal(1, figures.GetProperty("no").GetInt32());
    }

    /// <summary>
    /// The window is seven days, and the boundary is the part worth asserting: a test whose polls
    /// all sit far outside it passes just as happily against a window of sixty (found by mutating
    /// the constant during review, which the earlier version of this test survived).
    /// </summary>
    [Fact]
    public async Task Deletions_due_soon_counts_only_the_next_seven_days_and_names_the_earliest()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var soon = await SeedPollAsync(factory, admin, "Bald weg", 1);
        var justOutside = await SeedPollAsync(factory, admin, "Knapp daneben", 1);
        await SeedPollAsync(factory, admin, "Noch lange", 1);

        var due = DateTime.UtcNow.AddDays(3);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
            db.Polls.Single(p => p.Id == soon).RetentionDeadline = due;
            // Eight days out: inside any looser window, outside this one.
            db.Polls.Single(p => p.Id == justOutside).RetentionDeadline = DateTime.UtcNow.AddDays(8);
            await db.SaveChangesAsync();
        }

        var figures = await DashboardAsync(admin);

        Assert.Equal(1, figures.GetProperty("deletionsDueSoon").GetInt32());
        Assert.Equal(
            due.ToString("yyyy-MM-dd"),
            figures.GetProperty("nextDeletion").GetDateTime().ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task Polls_exist_but_none_is_due_soon_still_names_the_next_deletion()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        await SeedPollAsync(factory, admin, "Weit weg", 1);

        var figures = await DashboardAsync(admin);

        // The third reading the view must keep distinct: nothing due, but a date exists.
        Assert.Equal(0, figures.GetProperty("deletionsDueSoon").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, figures.GetProperty("nextDeletion").ValueKind);
    }

    /// <summary>
    /// FR-034. Nothing on this payload may identify a poll or a person. A dashboard is a place an
    /// operator leaves open, and it reports on other people's answers.
    /// </summary>
    [Fact]
    public async Task The_payload_carries_no_title_no_name_and_no_individual_answer()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        await SeedPollAsync(factory, admin, "Geheimer Grillabend", 1, [Availability.Yes]);

        var raw = await admin.GetStringAsync("/api/v1/admin/dashboard");

        Assert.DoesNotContain("Geheimer Grillabend", raw);
        Assert.DoesNotContain("Person", raw);

        // Exactly the eight fields of the contract, and nothing else. Asserted as a set, so a
        // ninth field carrying something identifying could not slip in unnoticed.
        var figures = JsonDocument.Parse(raw).RootElement;
        string[] expected =
        [
            "deletionsDueSoon", "maybe", "next"  + "Deletion", "no", "pollCount",
            "responseCount", "unansweredPolls", "yes",
        ];

        Assert.Equal(expected, figures.EnumerateObject().Select(p => p.Name).Order().ToArray());
    }

    [Fact]
    public async Task The_dashboard_refuses_without_a_session()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/api/v1/admin/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// FR-028b, asserted rather than trusted. The number of database round trips must not grow
    /// with the number of polls, which is what rules out reading each poll in turn.
    /// </summary>
    [Fact]
    public async Task The_number_of_queries_does_not_grow_with_the_number_of_polls()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        async Task<TimeSpan> TimeOneReadAsync()
        {
            var started = DateTime.UtcNow;
            await DashboardAsync(admin);
            return DateTime.UtcNow - started;
        }

        for (var i = 0; i < 3; i++)
        {
            await SeedPollAsync(factory, admin, $"Wenige {i}", 2, [Availability.Yes]);
        }

        var few = await TimeOneReadAsync();

        for (var i = 0; i < 30; i++)
        {
            await SeedPollAsync(factory, admin, $"Viele {i}", 2, [Availability.Yes]);
        }

        var many = await TimeOneReadAsync();

        // Eleven times the polls. A per-poll read would show it; five fixed aggregates do not.
        // The bound is deliberately loose - this asserts the shape of the cost, not a latency.
        Assert.True(
            many < few + TimeSpan.FromSeconds(1),
            $"reading 33 polls took {many.TotalMilliseconds:F0} ms against {few.TotalMilliseconds:F0} ms for 3");
    }
}
