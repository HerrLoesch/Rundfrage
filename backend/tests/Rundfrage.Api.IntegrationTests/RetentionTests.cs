using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rundfrage.Api.Data;
using Rundfrage.Api.Retention;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// User Story 5. Principle IV requires deletion to remove responses rather than hide them, so
/// every assertion here inspects storage directly - a 404 only proves unreachability.
/// </summary>
public class RetentionTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private sealed record Answered(string PollToken, Guid PollId, Guid ResponseId, string EditToken);

    private static async Task<Answered> AnswerAsync(ApiFactory factory)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var created = await admin.PostAsJsonAsync(
            "/api/v1/admin/polls", new { title = "Aufbewahrung", days = new[] { "2026-11-18" } });

        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        var pollToken = summary.GetProperty("participantToken").GetString()!;
        var pollId = summary.GetProperty("id").GetGuid();

        var anonymous = factory.CreateClient();
        var view = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/polls/{pollToken}");
        var dayId = view.GetProperty("days").EnumerateArray().First().GetProperty("id").GetGuid();

        var submitted = await anonymous.PostAsJsonAsync($"/api/v1/polls/{pollToken}/responses", new
        {
            displayName = "Anna",
            answers = new[] { new { dayId, availability = "yes" } },
        });

        var accepted = await submitted.Content.ReadFromJsonAsync<JsonElement>();

        return new Answered(pollToken, pollId,
            accepted.GetProperty("responseId").GetGuid(),
            accepted.GetProperty("editToken").GetString()!);
    }

    [Fact]
    public async Task Deleting_a_poll_removes_its_responses_from_storage()
    {
        // FR-037, FR-049, SC-007. Checked in the database, not by asking the API.
        using var factory = new ApiFactory(storage.DataDirectory);
        var answered = await AnswerAsync(factory);
        var admin = await factory.CreateSignedInClientAsync();

        var deleted = await admin.DeleteAsync($"/api/v1/admin/polls/{answered.PollId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();

        Assert.False(await db.Polls.AnyAsync(p => p.Id == answered.PollId));
        Assert.False(await db.Responses.AnyAsync(r => r.PollId == answered.PollId));
        Assert.False(await db.DayAnswers.AnyAsync(a => a.ResponseId == answered.ResponseId));
        Assert.False(await db.CandidateDays.AnyAsync(d => d.PollId == answered.PollId));
    }

    [Fact]
    public async Task After_deletion_both_link_kinds_produce_the_neutral_not_found()
    {
        // FR-040
        using var factory = new ApiFactory(storage.DataDirectory);
        var answered = await AnswerAsync(factory);
        var admin = await factory.CreateSignedInClientAsync();

        await admin.DeleteAsync($"/api/v1/admin/polls/{answered.PollId}");

        var anonymous = factory.CreateClient();
        var poll = await anonymous.GetAsync($"/api/v1/polls/{answered.PollToken}");
        var own = await anonymous.GetAsync($"/api/v1/responses/{answered.EditToken}");

        Assert.Equal("{\"code\":\"not_found\"}", await poll.Content.ReadAsStringAsync());
        Assert.Equal("{\"code\":\"not_found\"}", await own.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_single_response_can_be_deleted_without_touching_the_poll()
    {
        // FR-037a, FR-037b, SC-022: the remediation half of the abuse answer.
        using var factory = new ApiFactory(storage.DataDirectory);
        var first = await AnswerAsync(factory);

        var anonymous = factory.CreateClient();
        var view = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/polls/{first.PollToken}");
        var dayId = view.GetProperty("days").EnumerateArray().First().GetProperty("id").GetGuid();
        await anonymous.PostAsJsonAsync($"/api/v1/polls/{first.PollToken}/responses", new
        {
            displayName = "Bernd",
            answers = new[] { new { dayId, availability = "no" } },
        });

        var admin = await factory.CreateSignedInClientAsync();
        var deleted = await admin.DeleteAsync(
            $"/api/v1/admin/polls/{first.PollId}/responses/{first.ResponseId}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var after = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/polls/{first.PollToken}");
        Assert.Equal(1, after.GetProperty("responseCount").GetInt32());
        Assert.Equal("Bernd", after.GetProperty("responses").EnumerateArray()
            .Single().GetProperty("displayName").GetString());

        // FR-037b: the totals moved with it.
        var totals = after.GetProperty("totals").EnumerateArray().Single();
        Assert.Equal(0, totals.GetProperty("yes").GetInt32());
        Assert.Equal(1, totals.GetProperty("no").GetInt32());
    }

    [Fact]
    public async Task A_deleted_response_loses_its_personal_link()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var answered = await AnswerAsync(factory);
        var admin = await factory.CreateSignedInClientAsync();

        await admin.DeleteAsync($"/api/v1/admin/polls/{answered.PollId}/responses/{answered.ResponseId}");

        var own = await factory.CreateClient().GetAsync($"/api/v1/responses/{answered.EditToken}");

        Assert.Equal(HttpStatusCode.NotFound, own.StatusCode);
    }

    [Fact]
    public async Task A_poll_one_second_past_its_deadline_is_already_unreachable()
    {
        // SC-031: expiry takes effect on access, before any sweep has run.
        using var factory = new ApiFactory(storage.DataDirectory);
        var answered = await AnswerAsync(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
            await db.Polls.Where(p => p.Id == answered.PollId)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    p => p.RetentionDeadline, DateTime.UtcNow.AddSeconds(-1)));
        }

        var anonymous = factory.CreateClient();
        var admin = await factory.CreateSignedInClientAsync();

        Assert.Equal(HttpStatusCode.NotFound,
            (await anonymous.GetAsync($"/api/v1/polls/{answered.PollToken}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await anonymous.GetAsync($"/api/v1/responses/{answered.EditToken}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync($"/api/v1/admin/polls/{answered.PollId}")).StatusCode);

        // Still present in storage: unreachable is not the same as erased.
        using var verify = factory.Services.CreateScope();
        var check = verify.ServiceProvider.GetRequiredService<RundfrageDbContext>();
        Assert.True(await check.Polls.IgnoreQueryFilters().AnyAsync(p => p.Id == answered.PollId));
    }

    [Fact]
    public async Task The_sweep_erases_what_the_filter_has_already_hidden()
    {
        // FR-039c, SC-014, SC-032.
        using var factory = new ApiFactory(storage.DataDirectory);
        var answered = await AnswerAsync(factory);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
        await db.Polls.Where(p => p.Id == answered.PollId)
            .ExecuteUpdateAsync(s => s.SetProperty(
                p => p.RetentionDeadline, DateTime.UtcNow.AddSeconds(-1)));

        var retention = scope.ServiceProvider.GetRequiredService<RetentionService>();
        var removed = await retention.EraseExpiredAsync(CancellationToken.None);

        Assert.True(removed >= 1);
        Assert.False(await db.Polls.AnyAsync(p => p.Id == answered.PollId));
        Assert.False(await db.Responses.AnyAsync(r => r.PollId == answered.PollId));
    }

    [Fact]
    public async Task The_sweep_is_safe_to_run_repeatedly()
    {
        // FR-039d: a second run finds nothing left to do rather than failing.
        using var factory = new ApiFactory(storage.DataDirectory);
        var answered = await AnswerAsync(factory);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
        await db.Polls.Where(p => p.Id == answered.PollId)
            .ExecuteUpdateAsync(s => s.SetProperty(
                p => p.RetentionDeadline, DateTime.UtcNow.AddSeconds(-1)));

        var retention = scope.ServiceProvider.GetRequiredService<RetentionService>();
        await retention.EraseExpiredAsync(CancellationToken.None);
        var secondRun = await Record.ExceptionAsync(
            () => retention.EraseExpiredAsync(CancellationToken.None));

        Assert.Null(secondRun);
    }

    [Fact]
    public async Task Deleting_an_unknown_poll_gives_the_neutral_not_found()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var response = await admin.DeleteAsync("/api/v1/admin/polls/0199a000-0000-7000-8000-000000000000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("{\"code\":\"not_found\"}", await response.Content.ReadAsStringAsync());
    }
}
