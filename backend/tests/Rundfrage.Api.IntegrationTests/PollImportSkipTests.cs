using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Rundfrage.Api.Data.Entities;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// T017 and T018: what cannot be taken is skipped and reported, and the rest still arrives
/// (FR-012, FR-004, SC-003).
/// </summary>
/// <remarks>
/// Skipping is allowed; skipping quietly is not. Every case here asserts both halves - that the
/// item is absent, and that the summary says so. A test that only checked the first would pass
/// against an import that silently drops data, which is the failure this feature exists to avoid.
/// </remarks>
public class PollImportSkipTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    [Fact]
    public async Task A_poll_already_past_its_retention_date_is_skipped_and_reported()
    {
        using var factory = new ApiFactory(storage.DataDirectory);

        var (response, body) = await ImportTestHelper.ImportAsync(
            factory, new ExportDocumentBuilder().WithExpiredDays().WithResponse("Anna"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.GetProperty("imported").GetBoolean());
        Assert.Contains("already_expired", ImportTestHelper.SkipReasons(body));

        var skipped = body.GetProperty("skipped").EnumerateArray().First();
        Assert.Equal("poll", skipped.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Nothing_taken_is_reported_as_nothing_taken_and_not_as_an_error()
    {
        // FR-004. This is a specified outcome, not a failure: the status is 200 and the body says
        // plainly that no poll was created.
        using var factory = new ApiFactory(storage.DataDirectory);

        var (response, body) = await ImportTestHelper.ImportAsync(
            factory, new ExportDocumentBuilder().WithExpiredDays());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.GetProperty("imported").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("pollId").ValueKind);
        Assert.NotEmpty(body.GetProperty("skipped").EnumerateArray());
    }

    [Fact]
    public async Task An_answer_naming_a_day_the_poll_does_not_have_is_skipped()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var builder = new ExportDocumentBuilder();
        var stranger = builder.Days[0].AddYears(1);

        builder.WithRawResponse("Anna", [(builder.Days[0], "yes"), (stranger, "yes")]);

        var (response, body) = await ImportTestHelper.ImportAsync(factory, builder);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.GetProperty("imported").GetBoolean());
        Assert.Equal(1, body.GetProperty("counts").GetProperty("answers").GetInt32());
        Assert.Contains("unknown_day", ImportTestHelper.SkipReasons(body));

        var skipped = body.GetProperty("skipped").EnumerateArray()
            .First(s => s.GetProperty("reason").GetString() == "unknown_day");
        Assert.Equal("answer", skipped.GetProperty("kind").GetString());
        Assert.Contains(stranger.ToString("yyyy-MM-dd"), skipped.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task An_unrecognised_availability_is_skipped()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var builder = new ExportDocumentBuilder();
        builder.WithRawResponse("Anna", [(builder.Days[0], "yes"), (builder.Days[1], "perhaps")]);

        var (_, body) = await ImportTestHelper.ImportAsync(factory, builder);

        Assert.True(body.GetProperty("imported").GetBoolean());
        Assert.Equal(1, body.GetProperty("counts").GetProperty("answers").GetInt32());
        Assert.Contains("unknown_availability", ImportTestHelper.SkipReasons(body));
    }

    [Fact]
    public async Task A_display_name_over_the_limit_skips_that_response_only()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var builder = new ExportDocumentBuilder()
            .WithResponse("Anna")
            .WithResponse(ExportDocumentBuilder.OverLimit(PollResponse.DisplayNameMaxLength));

        var (_, body) = await ImportTestHelper.ImportAsync(factory, builder);

        Assert.True(body.GetProperty("imported").GetBoolean());
        Assert.Equal(1, body.GetProperty("counts").GetProperty("responses").GetInt32());
        Assert.Contains("name_too_long", ImportTestHelper.SkipReasons(body));

        var skipped = body.GetProperty("skipped").EnumerateArray()
            .First(s => s.GetProperty("reason").GetString() == "name_too_long");
        Assert.Equal("response", skipped.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task A_response_answering_the_same_day_twice_is_skipped_entirely()
    {
        // No rule picks a winner between two conflicting answers, so the response is not guessed at.
        using var factory = new ApiFactory(storage.DataDirectory);
        var builder = new ExportDocumentBuilder().WithResponse("Anna");
        builder.WithRawResponse("Bert", [(builder.Days[0], "yes"), (builder.Days[0], "no")]);

        var (_, body) = await ImportTestHelper.ImportAsync(factory, builder);

        Assert.True(body.GetProperty("imported").GetBoolean());
        Assert.Equal(1, body.GetProperty("counts").GetProperty("responses").GetInt32());
        Assert.Contains("duplicate_day", ImportTestHelper.SkipReasons(body));

        var token = body.GetProperty("participantToken").GetString()!;
        var view = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{token}");
        var names = view.GetProperty("responses").EnumerateArray()
            .Select(r => r.GetProperty("displayName").GetString()).ToArray();

        Assert.Equal(new[] { "Anna" }, names!);
    }

    [Fact]
    public async Task Duplicate_candidate_days_are_folded_rather_than_skipped()
    {
        // 002 FR-012 already says selecting the same day twice is one day. The import inherits
        // that rather than inventing a second rule.
        using var factory = new ApiFactory(storage.DataDirectory);
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(40);

        var (_, body) = await ImportTestHelper.ImportAsync(
            factory, new ExportDocumentBuilder().WithDays(day, day, day.AddDays(1)));

        Assert.True(body.GetProperty("imported").GetBoolean());
        Assert.Equal(2, body.GetProperty("counts").GetProperty("days").GetInt32());
    }

    [Fact]
    public async Task A_file_with_no_responses_imports_as_an_empty_poll()
    {
        using var factory = new ApiFactory(storage.DataDirectory);

        var (response, body) = await ImportTestHelper.ImportAsync(factory, new ExportDocumentBuilder());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.GetProperty("imported").GetBoolean());
        Assert.Equal(0, body.GetProperty("counts").GetProperty("responses").GetInt32());
        Assert.Empty(body.GetProperty("skipped").EnumerateArray());
    }

    [Fact]
    public async Task Everything_else_still_arrives_when_one_item_is_skipped()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var builder = new ExportDocumentBuilder().WithResponse("Anna").WithResponse("Bert");
        builder.WithRawResponse("Clara", [(builder.Days[0].AddYears(5), "yes")]);

        var (_, body) = await ImportTestHelper.ImportAsync(factory, builder);

        Assert.True(body.GetProperty("imported").GetBoolean());
        Assert.Equal(3, body.GetProperty("counts").GetProperty("responses").GetInt32());
        Assert.Equal(6, body.GetProperty("counts").GetProperty("answers").GetInt32());
        Assert.Contains("unknown_day", ImportTestHelper.SkipReasons(body));
    }
}
