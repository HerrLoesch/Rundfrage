using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rundfrage.Api.Data;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// User Story 4. Principle I forbids solving this by identifying the participant, so the
/// capability is a token and nothing else (FR-028 to FR-031).
/// </summary>
public class RevisionTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private sealed record Answered(string PollToken, Guid PollId, Guid[] DayIds, string EditToken, Guid ResponseId);

    private static async Task<Answered> AnswerAsync(ApiFactory factory, string name = "Anna")
    {
        var admin = await factory.CreateSignedInClientAsync();
        var created = await admin.PostAsJsonAsync(
            "/api/v1/admin/polls", new { title = "Korrektur", days = new[] { "2026-11-18", "2026-11-20" } });

        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        var pollToken = summary.GetProperty("participantToken").GetString()!;
        var pollId = summary.GetProperty("id").GetGuid();

        var anonymous = factory.CreateClient();
        var view = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/polls/{pollToken}");
        var dayIds = view.GetProperty("days").EnumerateArray()
            .Select(d => d.GetProperty("id").GetGuid()).ToArray();

        var submitted = await anonymous.PostAsJsonAsync($"/api/v1/polls/{pollToken}/responses", new
        {
            displayName = name,
            answers = new[] { new { dayId = dayIds[0], availability = "yes" } },
        });

        var accepted = await submitted.Content.ReadFromJsonAsync<JsonElement>();

        return new Answered(pollToken, pollId, dayIds,
            accepted.GetProperty("editToken").GetString()!,
            accepted.GetProperty("responseId").GetGuid());
    }

    [Fact]
    public async Task The_personal_link_returns_the_previous_answers_prefilled()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var answered = await AnswerAsync(factory);

        var own = await factory.CreateClient()
            .GetFromJsonAsync<JsonElement>($"/api/v1/responses/{answered.EditToken}");

        Assert.Equal("Anna", own.GetProperty("displayName").GetString());
        var answers = own.GetProperty("answers").EnumerateArray().ToArray();
        Assert.Single(answers);
        Assert.Equal("yes", answers[0].GetProperty("availability").GetString());
        // The poll comes with it, so the form can be rendered from one request.
        Assert.Equal("Korrektur", own.GetProperty("poll").GetProperty("title").GetString());
    }

    [Fact]
    public async Task A_revision_updates_in_place_and_creates_no_second_response()
    {
        // FR-030 and SC-008.
        using var factory = new ApiFactory(storage.DataDirectory);
        var answered = await AnswerAsync(factory);
        var anonymous = factory.CreateClient();

        var revised = await anonymous.PutAsJsonAsync($"/api/v1/responses/{answered.EditToken}", new
        {
            displayName = "Anna",
            answers = new[]
            {
                new { dayId = answered.DayIds[0], availability = "no" },
                new { dayId = answered.DayIds[1], availability = "maybe" },
            },
        });

        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);

        var view = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/polls/{answered.PollToken}");
        Assert.Equal(1, view.GetProperty("responseCount").GetInt32());

        var row = view.GetProperty("responses").EnumerateArray().Single();
        var byDay = row.GetProperty("answers").EnumerateArray()
            .ToDictionary(a => a.GetProperty("dayId").GetGuid(), a => a.GetProperty("availability").GetString());
        Assert.Equal("no", byDay[answered.DayIds[0]]);
        Assert.Equal("maybe", byDay[answered.DayIds[1]]);
    }

    [Fact]
    public async Task Omitting_a_day_in_a_revision_clears_that_answer()
    {
        // research.md R-8: absence is the state, so clearing means removing the row.
        using var factory = new ApiFactory(storage.DataDirectory);
        var answered = await AnswerAsync(factory);

        await factory.CreateClient().PutAsJsonAsync($"/api/v1/responses/{answered.EditToken}",
            new { displayName = "Anna", answers = Array.Empty<object>() });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
        var remaining = await db.DayAnswers.CountAsync(a => a.ResponseId == answered.ResponseId);

        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task An_edit_token_grants_access_to_that_response_and_no_other()
    {
        // FR-029.
        using var factory = new ApiFactory(storage.DataDirectory);
        var first = await AnswerAsync(factory, "Anna");
        var second = await AnswerAsync(factory, "Bernd");

        var own = await factory.CreateClient()
            .GetFromJsonAsync<JsonElement>($"/api/v1/responses/{first.EditToken}");

        Assert.Equal(first.ResponseId, own.GetProperty("responseId").GetGuid());
        Assert.NotEqual(second.ResponseId, own.GetProperty("responseId").GetGuid());
    }

    [Fact]
    public async Task An_edit_token_grants_no_admin_access()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var answered = await AnswerAsync(factory);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Edit-Token", answered.EditToken);

        var response = await client.GetAsync("/api/v1/admin/polls");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("short")]
    public async Task An_unknown_edit_token_produces_the_neutral_not_found(string token)
    {
        using var factory = new ApiFactory(storage.DataDirectory);

        var response = await factory.CreateClient().GetAsync($"/api/v1/responses/{token}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("{\"code\":\"not_found\"}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_edit_token_for_an_expired_poll_stops_working()
    {
        // FR-040: both link kinds die together.
        using var factory = new ApiFactory(storage.DataDirectory);
        var answered = await AnswerAsync(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
            await db.Polls.Where(p => p.Id == answered.PollId)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    p => p.RetentionDeadline, DateTime.UtcNow.AddSeconds(-1)));
        }

        var response = await factory.CreateClient().GetAsync($"/api/v1/responses/{answered.EditToken}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("{\"code\":\"not_found\"}", await response.Content.ReadAsStringAsync());
    }
}
