using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rundfrage.Api.Data;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// User Story 2. This is where Principle I stops being a promise the constitution makes and
/// becomes behaviour a test proves.
/// </summary>
public class ParticipationTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private ApiFactory NewFactory() => new(storage.DataDirectory);

    private static async Task<(string Token, Guid[] DayIds)> CreatePollAsync(
        ApiFactory factory, params string[] days)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var created = await admin.PostAsJsonAsync(
            "/api/v1/admin/polls", new { title = "Terminfindung", message = "Wann?", days });

        created.EnsureSuccessStatusCode();
        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        var token = summary.GetProperty("participantToken").GetString()!;

        // Day ids come from the participant view - the only place they are published.
        var anonymous = factory.CreateClient();
        var view = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/polls/{token}");
        var dayIds = view.GetProperty("days").EnumerateArray()
            .Select(d => d.GetProperty("id").GetGuid()).ToArray();

        return (token, dayIds);
    }

    [Fact]
    public async Task A_participant_reads_a_poll_with_no_session_of_any_kind()
    {
        // FR-019, FR-020, Principle I. A brand-new client: no cookie, no header, no account.
        using var factory = NewFactory();
        var (token, _) = await CreatePollAsync(factory, "2026-11-20", "2026-11-18");

        var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync($"/api/v1/polls/{token}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var view = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Terminfindung", view.GetProperty("title").GetString());
        // FR-013: chronological regardless of the order they were selected.
        var dates = view.GetProperty("days").EnumerateArray()
            .Select(d => d.GetProperty("date").GetString() ?? string.Empty).ToArray();
        Assert.Equal(["2026-11-18", "2026-11-20"], dates);
    }

    [Fact]
    public async Task The_request_carries_no_credential_at_all()
    {
        // Guards the requirement rather than the happy path: if a cookie ever became necessary,
        // this is where it would show.
        using var factory = NewFactory();
        var (token, _) = await CreatePollAsync(factory, "2026-11-20");

        var anonymous = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/polls/{token}");
        var response = await anonymous.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(request.Headers.Contains("Cookie"));
        Assert.False(request.Headers.Contains("Authorization"));
    }

    [Theory]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaa")]   // well formed, unknown
    [InlineData("short")]                     // malformed
    [InlineData("")]                          // empty
    [InlineData("plus+slash/notbase64url!!")] // wrong alphabet
    public async Task Unknown_and_malformed_tokens_produce_the_identical_not_found(string token)
    {
        // SC-012. Also compared against a *deleted* poll below, so all four causes are covered.
        using var factory = NewFactory();
        var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync($"/api/v1/polls/{token}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("{\"code\":\"not_found\"}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_expired_poll_is_indistinguishable_from_one_that_never_existed()
    {
        // FR-039b and SC-012 together: expiry takes effect on access, and looks like nothing.
        using var factory = NewFactory();
        var (token, _) = await CreatePollAsync(factory, "2026-11-20");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
            await db.Polls.Where(p => p.ParticipantToken == token)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    p => p.RetentionDeadline, DateTime.UtcNow.AddSeconds(-1)));
        }

        var anonymous = factory.CreateClient();
        var expired = await anonymous.GetAsync($"/api/v1/polls/{token}");
        var neverExisted = await anonymous.GetAsync("/api/v1/polls/aaaaaaaaaaaaaaaaaaaaaa");

        Assert.Equal(HttpStatusCode.NotFound, expired.StatusCode);
        Assert.Equal(
            await neverExisted.Content.ReadAsStringAsync(),
            await expired.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_complete_response_is_recorded_and_returns_a_personal_link()
    {
        // FR-025, FR-026: one session, no account, and the only way back to the answer.
        using var factory = NewFactory();
        var (token, dayIds) = await CreatePollAsync(factory, "2026-11-20", "2026-11-18");

        var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync($"/api/v1/polls/{token}/responses", new
        {
            displayName = "Anna",
            answers = new[]
            {
                new { dayId = dayIds[0], availability = "yes" },
                new { dayId = dayIds[1], availability = "maybe" },
            },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var accepted = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(22, accepted.GetProperty("editToken").GetString()!.Length);
    }

    [Fact]
    public async Task An_omitted_day_stores_nothing_at_all()
    {
        // FR-024 and research.md R-8: absence is the *no answer* state, not a fourth value.
        using var factory = NewFactory();
        var (token, dayIds) = await CreatePollAsync(factory, "2026-11-20", "2026-11-18");

        var anonymous = factory.CreateClient();
        await anonymous.PostAsJsonAsync($"/api/v1/polls/{token}/responses", new
        {
            displayName = "Bernd",
            answers = new[] { new { dayId = dayIds[0], availability = "no" } },
        });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();

        // Scoped to this poll: the class shares one database across its tests, so a global
        // count would measure everyone else's answers too.
        var stored = await db.DayAnswers
            .CountAsync(a => a.Response!.DisplayName == "Bernd");

        Assert.Equal(1, stored);
    }

    [Fact]
    public async Task A_response_without_a_name_is_refused()
    {
        using var factory = NewFactory();
        var (token, _) = await CreatePollAsync(factory, "2026-11-20");

        var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync(
            $"/api/v1/polls/{token}/responses", new { displayName = "  ", answers = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("display_name_required", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_day_from_another_poll_is_refused()
    {
        // Silently skipping it would attach an answer where it does not belong.
        using var factory = NewFactory();
        var (token, _) = await CreatePollAsync(factory, "2026-11-20");
        var (_, otherDays) = await CreatePollAsync(factory, "2026-12-01");

        var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync($"/api/v1/polls/{token}/responses", new
        {
            displayName = "Christa",
            answers = new[] { new { dayId = otherDays[0], availability = "yes" } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unknown_day", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Two_participants_may_use_the_same_display_name()
    {
        // FR-022: a name is a label, never an identity.
        using var factory = NewFactory();
        var (token, dayIds) = await CreatePollAsync(factory, "2026-11-20");
        var anonymous = factory.CreateClient();

        var first = await anonymous.PostAsJsonAsync($"/api/v1/polls/{token}/responses", new
        {
            displayName = "Alex",
            answers = new[] { new { dayId = dayIds[0], availability = "yes" } },
        });
        var second = await anonymous.PostAsJsonAsync($"/api/v1/polls/{token}/responses", new
        {
            displayName = "Alex",
            answers = new[] { new { dayId = dayIds[0], availability = "no" } },
        });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var firstToken = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("editToken").GetString();
        var secondToken = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("editToken").GetString();
        Assert.NotEqual(firstToken, secondToken);
    }

    [Fact]
    public async Task Submitting_to_an_expired_poll_produces_the_neutral_not_found()
    {
        using var factory = NewFactory();
        var (token, dayIds) = await CreatePollAsync(factory, "2026-11-20");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
            await db.Polls.Where(p => p.ParticipantToken == token)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    p => p.RetentionDeadline, DateTime.UtcNow.AddSeconds(-1)));
        }

        var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync($"/api/v1/polls/{token}/responses", new
        {
            displayName = "Dora",
            answers = new[] { new { dayId = dayIds[0], availability = "yes" } },
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("{\"code\":\"not_found\"}", await response.Content.ReadAsStringAsync());
    }
}
