using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rundfrage.Api.Data;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// US2: one poll as JSON (FR-013 to FR-021a, SC-009 to SC-011).
/// </summary>
public class ExportTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private static async Task<(Guid PollId, string Token, Guid[] DayIds)> CreatePollAsync(
        ApiFactory factory, params string[] days)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var created = await admin.PostAsJsonAsync("/api/v1/admin/polls", new
        {
            title = "Grillabend",
            message = "Wer kann wann?",
            days = days.Length > 0 ? days : ["2026-11-20", "2026-11-21"],
        });

        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        var token = summary.GetProperty("participantToken").GetString()!;
        var id = summary.GetProperty("id").GetGuid();

        var view = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{token}");
        var dayIds = view.GetProperty("days").EnumerateArray()
            .Select(d => d.GetProperty("id").GetGuid()).ToArray();

        return (id, token, dayIds);
    }

    private static async Task AnswerAsync(
        ApiFactory factory, string token, string name, params (Guid Day, string Availability)[] answers)
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            $"/api/v1/polls/{token}/responses",
            new { displayName = name, answers = answers.Select(a => new { dayId = a.Day, availability = a.Availability }) });

        response.EnsureSuccessStatusCode();
    }

    private static async Task<(JsonElement Document, string Raw, HttpResponseMessage Response)>
        ExportAsync(ApiFactory factory, Guid pollId)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var response = await admin.GetAsync($"/api/v1/admin/polls/{pollId}/export");
        var raw = await response.Content.ReadAsStringAsync();

        // Parsed from the raw bytes rather than through a typed reader: FR-016 is about the file
        // being valid JSON that needs no repair, and a typed reader would hide a document that
        // only parses because the reader was lenient.
        return (JsonDocument.Parse(raw).RootElement, raw, response);
    }

    [Fact]
    public async Task An_export_carries_the_poll_its_days_in_order_and_every_answer()
    {
        // FR-014, SC-009.
        using var factory = new ApiFactory(storage.DataDirectory);
        var (pollId, token, dayIds) = await CreatePollAsync(factory, "2026-11-21", "2026-11-20");

        await AnswerAsync(factory, token, "Anna", (dayIds[0], "yes"), (dayIds[1], "maybe"));
        await AnswerAsync(factory, token, "Bert", (dayIds[0], "no"));

        var (document, _, response) = await ExportAsync(factory, pollId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, document.GetProperty("formatVersion").GetInt32());
        Assert.True(document.TryGetProperty("exportedAt", out var exportedAt));
        Assert.True(exportedAt.GetDateTime() > DateTime.UtcNow.AddMinutes(-5));

        var poll = document.GetProperty("poll");
        Assert.Equal("Grillabend", poll.GetProperty("title").GetString());
        Assert.Equal("Wer kann wann?", poll.GetProperty("message").GetString());

        // Chronological, not the order they were typed in - they were given reversed above.
        var dates = poll.GetProperty("days").EnumerateArray()
            .Select(d => d.GetProperty("date").GetString()!).ToArray();
        Assert.Equal(["2026-11-20", "2026-11-21"], dates);

        var responses = document.GetProperty("responses").EnumerateArray().ToArray();
        Assert.Equal(2, responses.Length);

        var anna = responses.Single(r => r.GetProperty("displayName").GetString() == "Anna");
        var annaAnswers = anna.GetProperty("answers").EnumerateArray()
            .ToDictionary(
                a => a.GetProperty("date").GetString()!,
                a => a.GetProperty("availability").GetString()!);

        // Addressed by date rather than by an internal identifier: an export outlives the system
        // that produced it, and an opaque id would make it unreadable on its own.
        //
        // dayIds comes back chronologically, so dayIds[0] is the 20th however the days were
        // typed in - which is the point of asserting on dates here rather than on positions.
        Assert.Equal("yes", annaAnswers["2026-11-20"]);
        Assert.Equal("maybe", annaAnswers["2026-11-21"]);
    }

    [Fact]
    public async Task An_export_carries_exactly_the_fields_the_contract_names_and_no_others()
    {
        // contracts/openapi.yaml declares additionalProperties: false throughout. That is a
        // promise about what a reader will *not* find, and it is the half a shape test usually
        // misses: asserting the fields that must be there says nothing about a field that
        // should not be, and the way a token would reach an export is by someone widening a
        // projection, not by someone adding it on purpose.
        using var factory = new ApiFactory(storage.DataDirectory);
        var (pollId, token, dayIds) = await CreatePollAsync(factory);
        await AnswerAsync(factory, token, "Frida", (dayIds[0], "yes"));

        var (document, _, _) = await ExportAsync(factory, pollId);

        static string[] NamesOf(JsonElement element) =>
            element.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();

        Assert.Equal(["exportedAt", "formatVersion", "poll", "responses"], NamesOf(document));

        var poll = document.GetProperty("poll");
        Assert.Equal(["days", "message", "title"], NamesOf(poll));
        Assert.Equal(["date"], NamesOf(poll.GetProperty("days")[0]));

        var response = document.GetProperty("responses")[0];
        Assert.Equal(["answers", "displayName"], NamesOf(response));
        Assert.Equal(["availability", "date"], NamesOf(response.GetProperty("answers")[0]));
    }

    [Fact]
    public async Task A_poll_without_a_message_still_carries_the_field_as_null()
    {
        // The contract types message as nullable rather than optional, so a reader may assume
        // the key is there. Omitting it would be a different document shape for the same poll.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var created = await admin.PostAsJsonAsync(
            "/api/v1/admin/polls", new { title = "Ohne Nachricht", days = new[] { "2026-11-20" } });
        var pollId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var (document, _, _) = await ExportAsync(factory, pollId);

        var message = document.GetProperty("poll").GetProperty("message");
        Assert.Equal(JsonValueKind.Null, message.ValueKind);
    }

    [Fact]
    public async Task An_export_contains_no_token_of_any_kind()
    {
        // FR-015, SC-010. Scanned against the tokens actually held in storage rather than
        // against a pattern for what a token looks like: a pattern check passes if the token
        // format ever changes, which is exactly when it would stop protecting anything.
        using var factory = new ApiFactory(storage.DataDirectory);
        var (pollId, token, dayIds) = await CreatePollAsync(factory);

        await AnswerAsync(factory, token, "Clara", (dayIds[0], "yes"));
        await AnswerAsync(factory, token, "Dora", (dayIds[1], "no"));

        var (_, raw, _) = await ExportAsync(factory, pollId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();

        var participantToken = await db.Polls.Where(p => p.Id == pollId)
            .Select(p => p.ParticipantToken).SingleAsync();
        var editTokens = await db.Responses.Where(r => r.PollId == pollId)
            .Select(r => r.EditToken).ToListAsync();

        Assert.DoesNotContain(participantToken, raw);
        Assert.NotEmpty(editTokens);

        foreach (var editToken in editTokens)
        {
            Assert.DoesNotContain(editToken, raw);
        }
    }

    [Fact]
    public async Task A_day_someone_did_not_answer_is_absent_rather_than_a_fourth_value()
    {
        // SC-009a. Absence is the state in storage; the export keeps that meaning instead of
        // claiming something was recorded that never was.
        using var factory = new ApiFactory(storage.DataDirectory);
        var (pollId, token, dayIds) = await CreatePollAsync(factory);

        await AnswerAsync(factory, token, "Emil", (dayIds[0], "yes"));

        var (document, raw, _) = await ExportAsync(factory, pollId);

        var answers = document.GetProperty("responses")[0].GetProperty("answers").EnumerateArray().ToArray();

        Assert.Single(answers);
        Assert.Equal("yes", answers[0].GetProperty("availability").GetString());
        Assert.DoesNotContain("noAnswer", raw);
        Assert.DoesNotContain("\"none\"", raw);
    }

    [Fact]
    public async Task An_unanswered_poll_exports_successfully_with_an_empty_list()
    {
        // FR-018: a valid export, not an error.
        using var factory = new ApiFactory(storage.DataDirectory);
        var (pollId, _, _) = await CreatePollAsync(factory);

        var (document, _, response) = await ExportAsync(factory, pollId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Array, document.GetProperty("responses").ValueKind);
        Assert.Equal(0, document.GetProperty("responses").GetArrayLength());
    }

    [Fact]
    public async Task An_export_is_refused_without_a_creator_session()
    {
        // FR-017: refused exactly as every other admin function is, not more softly because the
        // result is "only" a download.
        using var factory = new ApiFactory(storage.DataDirectory);
        var (pollId, _, _) = await CreatePollAsync(factory);

        var response = await factory.CreateClient().GetAsync($"/api/v1/admin/polls/{pollId}/export");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_poll_gives_the_neutral_not_found()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var response = await admin.GetAsync($"/api/v1/admin/polls/{Guid.NewGuid()}/export");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("not_found", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_download_names_the_poll_and_the_moment()
    {
        // FR-021a: several exports must be able to share a folder without overwriting each other.
        using var factory = new ApiFactory(storage.DataDirectory);
        var (pollId, _, _) = await CreatePollAsync(factory);

        var (_, _, response) = await ExportAsync(factory, pollId);
        var disposition = response.Content.Headers.ContentDisposition;

        Assert.Equal("attachment", disposition?.DispositionType);
        var name = disposition?.FileNameStar ?? disposition?.FileName?.Trim('"') ?? "";
        Assert.StartsWith("grillabend-", name);
        Assert.EndsWith(".json", name);
    }

    [Fact]
    public async Task An_export_taken_while_someone_answers_never_contains_half_a_response()
    {
        // FR-019. A response and its answers are written together; an export assembled from
        // several reads could catch the row before its answers and produce a participant who
        // answered nothing - indistinguishable, in the file, from someone who chose not to.
        using var factory = new ApiFactory(storage.DataDirectory, submissionsPerHour: 10_000);
        var (pollId, token, dayIds) = await CreatePollAsync(factory);

        using var stop = new CancellationTokenSource();
        var writing = Task.Run(async () =>
        {
            var client = factory.CreateClient();
            for (var i = 0; !stop.IsCancellationRequested; i++)
            {
                await client.PostAsJsonAsync($"/api/v1/polls/{token}/responses", new
                {
                    displayName = $"Laufend {i}",
                    answers = new[]
                    {
                        new { dayId = dayIds[0], availability = "yes" },
                        new { dayId = dayIds[1], availability = "no" },
                    },
                });
            }
        }, CancellationToken.None);

        await Task.Delay(200);
        var (document, _, _) = await ExportAsync(factory, pollId);
        await stop.CancelAsync();
        await writing;

        var responses = document.GetProperty("responses").EnumerateArray().ToArray();
        Assert.NotEmpty(responses);

        // Everyone in the file answered both days, because that is the only thing anyone did.
        Assert.All(responses, r => Assert.Equal(2, r.GetProperty("answers").GetArrayLength()));
    }
}
