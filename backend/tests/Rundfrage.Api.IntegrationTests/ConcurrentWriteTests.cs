using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Security;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// FR-010, FR-029, SC-006: the response cap holds exactly when submissions arrive together.
/// </summary>
/// <remarks>
/// These guarantees passed against the previous storage, where a row lock enforced them. That
/// mechanism is gone. Re-running the old suite would prove nothing about the new one, so the
/// contention is provoked here rather than assumed away - the specification's reason for
/// rejecting plain files as the system of record rests on exactly this.
/// </remarks>
public class ConcurrentWriteTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private const int Attempts = 40;

    private static async Task<(string Token, Guid PollId, Guid[] DayIds)> CreatePollAsync(ApiFactory factory)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var created = await admin.PostAsJsonAsync(
            "/api/v1/admin/polls", new { title = "Gleichzeitig", days = new[] { "2026-11-20" } });

        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        var token = summary.GetProperty("participantToken").GetString()!;
        var id = summary.GetProperty("id").GetGuid();

        var view = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{token}");
        var dayIds = view.GetProperty("days").EnumerateArray()
            .Select(d => d.GetProperty("id").GetGuid()).ToArray();

        return (token, id, dayIds);
    }

    /// <summary>Fills the poll to one short of its cap without going through HTTP.</summary>
    private static async Task FillToAsync(ApiFactory factory, Guid pollId, int count)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
        db.Responses.AddRange(Enumerable.Range(0, count).Select(i => new PollResponse
        {
            Id = Guid.CreateVersion7(),
            PollId = pollId,
            DisplayName = $"Person {i}",
            EditToken = CapabilityToken.Mint(),
            SubmittedAt = DateTime.UtcNow,
        }));
        await db.SaveChangesAsync();
    }

    private static Task<HttpResponseMessage[]> SubmitTogetherAsync(
        ApiFactory factory, string token, Guid dayId, int count)
    {
        // One client per caller, started together: sharing a client would serialise them and the
        // test would pass without ever creating the contention it is named after.
        var gate = new TaskCompletionSource();

        var calls = Enumerable.Range(0, count).Select(async i =>
        {
            var client = factory.CreateClient();
            await gate.Task;

            return await client.PostAsJsonAsync($"/api/v1/polls/{token}/responses", new
            {
                displayName = $"Gleichzeitig {i}",
                answers = new[] { new { dayId, availability = "yes" } },
            });
        }).ToArray();

        gate.SetResult();
        return Task.WhenAll(calls);
    }

    [Fact]
    public async Task The_last_free_place_is_given_to_exactly_one_of_many_simultaneous_submissions()
    {
        // SC-006 and SC-006a. Cap minus one, then many at once: one may be accepted, the rest
        // must be refused, and none may fail in a way that is neither.
        using var factory = new ApiFactory(storage.DataDirectory, submissionsPerHour: 10_000);
        var (token, pollId, dayIds) = await CreatePollAsync(factory);
        await FillToAsync(factory, pollId, Poll.MaxResponses - 1);

        var responses = await SubmitTogetherAsync(factory, token, dayIds[0], Attempts);

        var accepted = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var refused = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, accepted);
        Assert.Equal(Attempts - 1, refused);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
        Assert.Equal(Poll.MaxResponses, await db.Responses.CountAsync(r => r.PollId == pollId));
    }

    [Fact]
    public async Task Simultaneous_submissions_to_a_poll_with_room_are_all_recorded()
    {
        // SC-006b and the first edge case in the specification: neither overwrites the other.
        // This is the half that a cap-only test would miss - refusing everything would satisfy
        // the test above and fail the group using the system.
        using var factory = new ApiFactory(storage.DataDirectory, submissionsPerHour: 10_000);
        var (token, pollId, dayIds) = await CreatePollAsync(factory);

        var responses = await SubmitTogetherAsync(factory, token, dayIds[0], Attempts);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
        Assert.Equal(Attempts, await db.Responses.CountAsync(r => r.PollId == pollId));

        // Every one of them is a separate response with its own edit token, not one row written
        // over and over.
        var tokens = await db.Responses.Where(r => r.PollId == pollId)
            .Select(r => r.EditToken).ToListAsync();
        Assert.Equal(Attempts, tokens.Distinct().Count());
    }
}
