using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Security;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// FR-018. The listing is what the operator acts on - and FR-038 makes its response count part
/// of the deletion confirmation, so a wrong count would tell someone "0 responses will be
/// destroyed" while destroying five hundred.
/// </summary>
public class PollListingTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private async Task<(ApiFactory Factory, HttpClient Client)> SignedInAsync()
    {
        var factory = new ApiFactory(postgres.ConnectionString);
        var client = await factory.CreateSignedInClientAsync();
        return (factory, client);
    }

    private static async Task AddResponsesAsync(ApiFactory factory, Guid pollId, int count)
    {
        // Inserted directly: the submission endpoint arrives with US2, and this test is about
        // what the listing reports, not about how a response got there.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();

        for (var i = 0; i < count; i++)
        {
            db.Responses.Add(new PollResponse
            {
                Id = Guid.CreateVersion7(),
                PollId = pollId,
                DisplayName = $"Person {i}",
                EditToken = CapabilityToken.Mint(),
                SubmittedAt = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<Guid> CreatePollAsync(HttpClient client, string title, params string[] days)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/polls", new { title, message = (string?)null, days });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task The_listing_reports_how_many_responses_a_poll_actually_has()
    {
        var (factory, client) = await SignedInAsync();
        using var _ = factory;

        var pollId = await CreatePollAsync(client, "Zaehltest", "2026-11-20");
        await AddResponsesAsync(factory, pollId, 3);

        var listing = await client.GetFromJsonAsync<JsonElement>("/api/v1/admin/polls");
        var poll = listing.EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == pollId);

        Assert.Equal(3, poll.GetProperty("responseCount").GetInt32());
    }

    [Fact]
    public async Task The_listing_reports_the_number_of_candidate_days()
    {
        var (factory, client) = await SignedInAsync();
        using var _ = factory;

        var pollId = await CreatePollAsync(client, "Tagezahl", "2026-11-20", "2026-11-18", "2026-11-18");

        var listing = await client.GetFromJsonAsync<JsonElement>("/api/v1/admin/polls");
        var poll = listing.EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == pollId);

        // FR-012: the repeated day counts once.
        Assert.Equal(2, poll.GetProperty("dayCount").GetInt32());
    }

    [Fact]
    public async Task A_poll_past_its_retention_deadline_is_not_listed()
    {
        // FR-039b: expiry takes effect on access, not when a sweep happens to run.
        var (factory, client) = await SignedInAsync();
        using var _ = factory;

        var pollId = await CreatePollAsync(client, "Abgelaufen", "2026-11-20");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
            await db.Polls.Where(p => p.Id == pollId)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    p => p.RetentionDeadline, DateTimeOffset.UtcNow.AddSeconds(-1)));
        }

        var listing = await client.GetFromJsonAsync<JsonElement>("/api/v1/admin/polls");

        Assert.DoesNotContain(listing.EnumerateArray(), p => p.GetProperty("id").GetGuid() == pollId);
    }

    [Fact]
    public async Task The_listing_never_exposes_anyone_s_edit_token()
    {
        // FR-029: the revision capability belongs to its holder, not to the operator.
        var (factory, client) = await SignedInAsync();
        using var _ = factory;

        var pollId = await CreatePollAsync(client, "Tokenpruefung", "2026-11-20");
        await AddResponsesAsync(factory, pollId, 2);

        var body = await client.GetStringAsync("/api/v1/admin/polls");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
        var tokens = await db.Responses.Where(r => r.PollId == pollId).Select(r => r.EditToken).ToListAsync();

        Assert.NotEmpty(tokens);
        Assert.All(tokens, token => Assert.DoesNotContain(token, body));
    }
}
