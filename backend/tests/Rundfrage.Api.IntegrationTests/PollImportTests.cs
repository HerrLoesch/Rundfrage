using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// US1: a JSON export becomes a new poll (FR-007 to FR-015).
/// </summary>
public class PollImportTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    [Fact]
    public async Task Refuses_without_an_operator_session()
    {
        // FR-002. Checked first, because everything below assumes the route is protected.
        using var factory = new ApiFactory(storage.DataDirectory);

        var response = await factory.CreateClient().PostAsync(
            ImportTestHelper.ImportRoute, new ExportDocumentBuilder().ToFormContent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Creates_a_poll_and_reports_what_it_took()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var document = new ExportDocumentBuilder()
            .WithTitle("Grillabend")
            .WithMessage("Wer kann wann?")
            .WithResponse("Anna");

        var (response, body) = await ImportTestHelper.ImportAsync(factory, document);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.GetProperty("imported").GetBoolean());
        Assert.Equal(3, body.GetProperty("counts").GetProperty("days").GetInt32());
        Assert.Equal(1, body.GetProperty("counts").GetProperty("responses").GetInt32());
        Assert.Equal(3, body.GetProperty("counts").GetProperty("answers").GetInt32());
        Assert.Empty(body.GetProperty("skipped").EnumerateArray());
    }

    [Fact]
    public async Task Reproduces_an_exported_poll_field_by_field()
    {
        // SC-002 and FR-009. Compared field by field rather than by counts: a row count that
        // matches says nothing about whether the right values arrived.
        using var factory = new ApiFactory(storage.DataDirectory);

        var (_, token, exportJson) = await ImportTestHelper.CreateAndExportAsync(
            factory, "Sommerfest", "Bitte bis Freitag", ["2027-06-11", "2027-06-12", "2027-06-13"]);

        var dayIds = await ImportTestHelper.DayIdsAsync(factory, token);
        await ImportTestHelper.AnswerAsync(factory, token, "Anna",
            (dayIds[0], "yes"), (dayIds[1], "maybe"), (dayIds[2], "no"));
        await ImportTestHelper.AnswerAsync(factory, token, "Bert", (dayIds[0], "no"));

        // Re-export so the document carries the answers, then import that.
        var admin = await factory.CreateSignedInClientAsync();
        var polls = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/polls");
        var pollId = polls.EnumerateArray().First().GetProperty("id").GetGuid();
        var withAnswers = await (await admin.GetAsync($"/api/v1/admin/polls/{pollId}/export"))
            .Content.ReadAsStringAsync();

        var (response, body) = await ImportTestHelper.ImportAsync(
            factory, ExportDocumentBuilder.FormContentFor(withAnswers));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.GetProperty("imported").GetBoolean());

        var importedToken = body.GetProperty("participantToken").GetString()!;
        var imported = await factory.CreateClient()
            .GetFromJsonAsync<JsonElement>($"/api/v1/polls/{importedToken}");

        Assert.Equal("Sommerfest", imported.GetProperty("title").GetString());
        Assert.Equal("Bitte bis Freitag", imported.GetProperty("message").GetString());

        var importedDays = imported.GetProperty("days").EnumerateArray()
            .Select(d => d.GetProperty("date").GetString()).ToArray();
        Assert.Equal(new[] { "2027-06-11", "2027-06-12", "2027-06-13" }, importedDays!);

        // Re-export the imported poll and compare the two documents' meaningful halves.
        var reexported = JsonDocument.Parse(withAnswers).RootElement;
        var original = reexported.GetProperty("responses").EnumerateArray()
            .Select(r => (
                Name: r.GetProperty("displayName").GetString(),
                Answers: r.GetProperty("answers").EnumerateArray()
                    .Select(a => $"{a.GetProperty("date").GetString()}:{a.GetProperty("availability").GetString()}")
                    .OrderBy(x => x).ToArray()))
            .OrderBy(r => r.Name).ToArray();

        var importedPollId = body.GetProperty("pollId").GetGuid();
        var roundTripped = JsonDocument.Parse(
            await (await admin.GetAsync($"/api/v1/admin/polls/{importedPollId}/export"))
                .Content.ReadAsStringAsync()).RootElement;

        var actual = roundTripped.GetProperty("responses").EnumerateArray()
            .Select(r => (
                Name: r.GetProperty("displayName").GetString(),
                Answers: r.GetProperty("answers").EnumerateArray()
                    .Select(a => $"{a.GetProperty("date").GetString()}:{a.GetProperty("availability").GetString()}")
                    .OrderBy(x => x).ToArray()))
            .OrderBy(r => r.Name).ToArray();

        // Guard against the vacuous pass: with two empty sequences every assertion below is
        // trivially true, and the test would keep passing against an import that dropped
        // everything.
        Assert.Equal(2, original.Length);
        Assert.Equal(original.Length, actual.Length);

        for (var i = 0; i < original.Length; i++)
        {
            Assert.Equal(original[i].Name, actual[i].Name);
            Assert.Equal(original[i].Answers, actual[i].Answers);
        }
    }

    [Fact]
    public async Task Gives_the_imported_poll_a_link_the_original_never_had()
    {
        // FR-011. The export carries no token by design (003 FR-015), so continuity is impossible
        // - and the operator has to be able to see that it did not happen.
        using var factory = new ApiFactory(storage.DataDirectory);

        var (_, originalToken, exportJson) = await ImportTestHelper.CreateAndExportAsync(
            factory, "Kegeln", null, ["2027-03-04"]);

        var (_, body) = await ImportTestHelper.ImportAsync(
            factory, ExportDocumentBuilder.FormContentFor(exportJson));

        var importedToken = body.GetProperty("participantToken").GetString()!;

        Assert.NotEqual(originalToken, importedToken);
        Assert.DoesNotContain(originalToken, exportJson);
    }

    [Fact]
    public async Task A_participant_can_answer_an_imported_poll_and_sees_the_imported_answers()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var document = new ExportDocumentBuilder().WithResponse("Anna", "yes");

        var (_, body) = await ImportTestHelper.ImportAsync(factory, document);
        var token = body.GetProperty("participantToken").GetString()!;

        var dayIds = await ImportTestHelper.DayIdsAsync(factory, token);
        await ImportTestHelper.AnswerAsync(factory, token, "Bert", (dayIds[0], "no"));

        var view = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{token}");
        var names = view.GetProperty("responses").EnumerateArray()
            .Select(r => r.GetProperty("displayName").GetString()).ToArray();

        Assert.Contains("Anna", names);
        Assert.Contains("Bert", names);
    }

    [Fact]
    public async Task A_day_nobody_answered_stays_unanswered()
    {
        // FR-010: absence is the state, in the file and in storage. No fourth value is invented.
        using var factory = new ApiFactory(storage.DataDirectory);
        var builder = new ExportDocumentBuilder();
        var onlyFirstDay = builder.Days.Take(1).ToArray();
        builder.WithResponse("Anna", "yes", onlyFirstDay);

        var (_, body) = await ImportTestHelper.ImportAsync(factory, builder);

        Assert.Equal(1, body.GetProperty("counts").GetProperty("answers").GetInt32());

        var token = body.GetProperty("participantToken").GetString()!;
        var view = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{token}");
        var answers = view.GetProperty("responses").EnumerateArray().First().GetProperty("answers");

        Assert.Single(answers.EnumerateArray());
    }

    [Fact]
    public async Task The_same_file_imported_twice_yields_two_independent_polls()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var json = new ExportDocumentBuilder().WithTitle("Doppelt").WithResponse("Anna").ToJson();

        var (_, first) = await ImportTestHelper.ImportAsync(
            factory, ExportDocumentBuilder.FormContentFor(json));
        var (_, second) = await ImportTestHelper.ImportAsync(
            factory, ExportDocumentBuilder.FormContentFor(json));

        Assert.NotEqual(first.GetProperty("pollId").GetGuid(), second.GetProperty("pollId").GetGuid());
        Assert.NotEqual(
            first.GetProperty("participantToken").GetString(),
            second.GetProperty("participantToken").GetString());

        // Neither damaged the other: both are reachable and both hold their response.
        foreach (var body in new[] { first, second })
        {
            var token = body.GetProperty("participantToken").GetString()!;
            var view = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{token}");
            Assert.Single(view.GetProperty("responses").EnumerateArray());
        }
    }

    [Fact]
    public async Task The_imported_poll_gets_a_retention_deadline_and_appears_in_the_listing()
    {
        // FR-015: by the ordinary rule, and visible where every other poll's deadline is visible.
        using var factory = new ApiFactory(storage.DataDirectory);
        var builder = new ExportDocumentBuilder();
        var lastDay = builder.Days[^1];

        var (_, body) = await ImportTestHelper.ImportAsync(factory, builder);
        var pollId = body.GetProperty("pollId").GetGuid();

        var admin = await factory.CreateSignedInClientAsync();
        var listing = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/polls");
        var row = listing.EnumerateArray().First(p => p.GetProperty("id").GetGuid() == pollId);

        var deadline = row.GetProperty("retentionDeadline").GetDateTime();
        var expected = lastDay.ToDateTime(TimeOnly.MinValue).AddDays(30);

        Assert.True(
            Math.Abs((deadline - expected).TotalDays) < 2,
            $"deadline {deadline:o} should follow the last candidate day + 30 days, near {expected:o}");
    }
}
