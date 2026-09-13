using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rundfrage.Api.Data;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 008 FR-017a and SC-002: an item holds exactly as many names as are wanted, whatever order
/// simultaneous claims arrive in.
/// </summary>
/// <remarks>
/// This is the requirement that cannot be retrofitted, so this file is written before
/// <c>ClaimService</c> exists. A capacity check outside a transaction passes every sequential
/// test and fails exactly here.
/// <para>
/// Modelled on <see cref="ConcurrentWriteTests"/>, including its reason for one client per
/// caller: sharing a client would serialise the callers and the test would pass without ever
/// creating the contention it is named after.
/// </para>
/// </remarks>
public class WishConcurrencyTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private const int Attempts = 100;

    private static async Task<(string ListToken, Guid ListId, Guid ItemId)> ShareAsync(
        ApiFactory factory, int wantedCount)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var created = await admin.PostAsJsonAsync("/api/v1/admin/wish-lists", new
        {
            title = "Gleichzeitig",
            targetDate = "2099-07-18",
            items = new[] { new { name = "Kuchen", wantedCount } },
        });

        created.EnsureSuccessStatusCode();
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();

        return (
            body.GetProperty("listToken").GetString()!,
            body.GetProperty("id").GetGuid(),
            body.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid());
    }

    private static Task<HttpResponseMessage[]> ClaimTogetherAsync(
        ApiFactory factory, string listToken, Guid itemId, int count)
    {
        var gate = new TaskCompletionSource();

        var calls = Enumerable.Range(0, count).Select(async i =>
        {
            var client = factory.CreateClient();
            await gate.Task;

            return await client.PostAsJsonAsync($"/api/v1/wish-lists/{listToken}", new
            {
                displayName = $"Gleichzeitig {i}",
                itemIds = new[] { itemId },
            });
        }).ToArray();

        gate.SetResult();
        return Task.WhenAll(calls);
    }

    private static async Task<int> ClaimCountAsync(ApiFactory factory, Guid itemId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
        return await db.WishClaims.CountAsync(c => c.WishItemId == itemId);
    }

    [Fact]
    public async Task The_last_free_place_is_given_to_exactly_one_of_a_hundred_simultaneous_claims()
    {
        using var factory = new ApiFactory(storage.DataDirectory, submissionsPerHour: 10_000);
        var (listToken, _, itemId) = await ShareAsync(factory, wantedCount: 1);

        var responses = await ClaimTogetherAsync(factory, listToken, itemId, Attempts);

        var accepted = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var refused = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, accepted);
        Assert.Equal(Attempts - 1, refused);
        Assert.Equal(1, await ClaimCountAsync(factory, itemId));
    }

    [Fact]
    public async Task An_item_wanted_n_times_never_holds_more_than_n()
    {
        // The general statement of FR-017a: not "one wins" but "the count is the capacity".
        const int wanted = 5;

        using var factory = new ApiFactory(storage.DataDirectory, submissionsPerHour: 10_000);
        var (listToken, _, itemId) = await ShareAsync(factory, wanted);

        var responses = await ClaimTogetherAsync(factory, listToken, itemId, Attempts);

        Assert.Equal(wanted, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(Attempts - wanted, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(wanted, await ClaimCountAsync(factory, itemId));
    }

    [Fact]
    public async Task Simultaneous_claims_on_an_item_with_room_are_all_recorded()
    {
        // The half a cap-only test would miss: refusing everything would satisfy the two above
        // and fail the group using the system.
        using var factory = new ApiFactory(storage.DataDirectory, submissionsPerHour: 10_000);
        var (listToken, _, itemId) = await ShareAsync(factory, wantedCount: WishItemCapacity);

        var responses = await ClaimTogetherAsync(factory, listToken, itemId, WishItemCapacity);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal(WishItemCapacity, await ClaimCountAsync(factory, itemId));

        // Each is its own row with its own personal token: one submission, one token, and these
        // were separate submissions (FR-022b).
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
        var tokens = await db.WishClaims.Where(c => c.WishItemId == itemId)
            .Select(c => c.ClaimToken).ToListAsync();
        Assert.Equal(WishItemCapacity, tokens.Distinct().Count());
    }

    private const int WishItemCapacity = Rundfrage.Api.Data.Entities.WishItem.MaxWantedCount;
}
