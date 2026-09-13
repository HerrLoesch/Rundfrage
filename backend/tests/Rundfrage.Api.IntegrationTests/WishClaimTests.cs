using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rundfrage.Api.Data;
using Rundfrage.Api.Http;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// User Story 1 from the participant's side: reading a wish list from a bare link, claiming
/// items, and the personal link that comes back (Principle I, 008 FR-013 to FR-024).
/// </summary>
public class WishClaimTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private sealed record Shared(string ListToken, Guid ListId, Dictionary<string, Guid> Items);

    private static async Task<Shared> ShareAsync(
        ApiFactory factory, params (string Name, int? Wanted)[] items)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var response = await admin.PostAsJsonAsync("/api/v1/admin/wish-lists", new
        {
            title = "Sommerfest",
            description = "Bitte eintragen",
            targetDate = "2099-07-18",
            items = items.Length == 0
                ? [new { name = "Kuchen", wantedCount = (int?)2 }]
                : items.Select(i => new { name = i.Name, wantedCount = i.Wanted }).ToArray(),
        });

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();

        return new Shared(
            created.GetProperty("listToken").GetString()!,
            created.GetProperty("id").GetGuid(),
            created.GetProperty("items").EnumerateArray().ToDictionary(
                i => i.GetProperty("name").GetString()!, i => i.GetProperty("id").GetGuid()));
    }

    private static Task<HttpResponseMessage> ClaimAsync(
        HttpClient client, string listToken, string name, params Guid[] itemIds) =>
        client.PostAsJsonAsync($"/api/v1/wish-lists/{listToken}",
            new { displayName = name, itemIds });

    private static async Task<string> CodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task A_bare_link_shows_the_list_with_no_session_and_no_account()
    {
        // Principle I, FR-013, FR-014: no sign-in, nothing between the link and the form.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, ("Kuchen", 2), ("Grill", null));

        var view = await factory.CreateClient()
            .GetFromJsonAsync<JsonElement>($"/api/v1/wish-lists/{shared.ListToken}");

        Assert.Equal("Sommerfest", view.GetProperty("title").GetString());
        Assert.Equal("Bitte eintragen", view.GetProperty("description").GetString());
        Assert.Equal("2099-07-18", view.GetProperty("targetDate").GetString());
        Assert.False(view.GetProperty("closed").GetBoolean());

        var items = view.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(new[] { "Kuchen", "Grill" }, items.Select(i => i.GetProperty("name").GetString()));
        Assert.Equal(2, items[0].GetProperty("openPlaces").GetInt32());
        Assert.Empty(items[0].GetProperty("names").EnumerateArray());
    }

    [Fact]
    public async Task A_claim_is_stored_and_the_names_are_visible_to_every_link_holder()
    {
        // FR-019: seeing what is taken before choosing is the point of a bring-list.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, ("Kuchen", 3));
        var anonymous = factory.CreateClient();

        var claimed = await ClaimAsync(anonymous, shared.ListToken, "Anna", shared.Items["Kuchen"]);

        Assert.Equal(HttpStatusCode.Created, claimed.StatusCode);
        var result = await claimed.Content.ReadFromJsonAsync<JsonElement>();
        var item = result.GetProperty("list").GetProperty("items").EnumerateArray().First();

        Assert.Equal(2, item.GetProperty("openPlaces").GetInt32());
        Assert.Equal(new[] { "Anna" }, item.GetProperty("names").EnumerateArray()
            .Select(n => n.GetString()));

        // And to somebody else holding the same link.
        var elsewhere = await factory.CreateClient()
            .GetFromJsonAsync<JsonElement>($"/api/v1/wish-lists/{shared.ListToken}");
        Assert.Equal(new[] { "Anna" }, elsewhere.GetProperty("items").EnumerateArray().First()
            .GetProperty("names").EnumerateArray().Select(n => n.GetString()));
    }

    [Fact]
    public async Task Several_items_claimed_at_once_produce_one_personal_link()
    {
        // FR-016 and FR-022b: one submission, one link - not one per item.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, ("Kuchen", 2), ("Grill", null), ("Salat", 2));
        var anonymous = factory.CreateClient();

        var claimed = await ClaimAsync(anonymous, shared.ListToken, "Anna",
            shared.Items["Kuchen"], shared.Items["Grill"], shared.Items["Salat"]);

        var result = await claimed.Content.ReadFromJsonAsync<JsonElement>();
        var token = result.GetProperty("claimToken").GetString()!;

        var covered = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/claims/{token}");
        Assert.Equal(3, covered.GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public async Task An_item_accepts_exactly_as_many_names_as_are_wanted()
    {
        // FR-017, and the refusal carries the item's current state (FR-017b).
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, ("Kuchen", 2), ("Grill", null));
        var anonymous = factory.CreateClient();

        await ClaimAsync(anonymous, shared.ListToken, "Anna", shared.Items["Kuchen"]);
        await ClaimAsync(anonymous, shared.ListToken, "Ben", shared.Items["Kuchen"]);

        var third = await ClaimAsync(anonymous, shared.ListToken, "Chris", shared.Items["Kuchen"]);

        // 409, the same status 002 answers for poll_full: "there is no room" reads the same way
        // everywhere in this system.
        Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);
        var body = await third.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ErrorCodes.ItemFull, body.GetProperty("code").GetString());
        Assert.Equal(0, body.GetProperty("item").GetProperty("openPlaces").GetInt32());

        // A different item on the same list is unaffected.
        var other = await ClaimAsync(anonymous, shared.ListToken, "Chris", shared.Items["Grill"]);
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);
    }

    [Fact]
    public async Task The_same_name_may_appear_twice_on_one_item()
    {
        // FR-020: two people called Anna may both bring a cake. A name is a label.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, ("Kuchen", 2));
        var anonymous = factory.CreateClient();

        await ClaimAsync(anonymous, shared.ListToken, "Anna", shared.Items["Kuchen"]);
        var second = await ClaimAsync(anonymous, shared.ListToken, "Anna", shared.Items["Kuchen"]);

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Theory]
    [InlineData("", ErrorCodes.DisplayNameRequired)]
    [InlineData("   ", ErrorCodes.DisplayNameRequired)]
    public async Task A_claim_without_a_name_is_refused(string name, string expected)
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, ("Kuchen", 2));

        var response = await ClaimAsync(
            factory.CreateClient(), shared.ListToken, name, shared.Items["Kuchen"]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expected, await CodeAsync(response));
    }

    [Fact]
    public async Task A_name_beyond_its_limit_is_refused()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, ("Kuchen", 2));

        var response = await ClaimAsync(factory.CreateClient(), shared.ListToken,
            new string('x', 101), shared.Items["Kuchen"]);

        Assert.Equal(ErrorCodes.DisplayNameTooLong, await CodeAsync(response));
    }

    [Fact]
    public async Task An_item_from_another_list_is_refused_rather_than_skipped()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var one = await ShareAsync(factory, ("Kuchen", 2));
        var other = await ShareAsync(factory, ("Salat", 2));

        var response = await ClaimAsync(
            factory.CreateClient(), one.ListToken, "Anna", other.Items["Salat"]);

        Assert.Equal(ErrorCodes.UnknownItem, await CodeAsync(response));
    }

    [Fact]
    public async Task Unknown_malformed_and_deleted_tokens_answer_identically()
    {
        // FR-024 and SC-012: an observer holding an old link cannot tell deletion from invalidity.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, ("Kuchen", 2));
        var admin = await factory.CreateSignedInClientAsync();
        await admin.DeleteAsync($"/api/v1/admin/wish-lists/{shared.ListId}");

        var anonymous = factory.CreateClient();
        var deleted = await anonymous.GetAsync($"/api/v1/wish-lists/{shared.ListToken}");
        var unknown = await anonymous.GetAsync("/api/v1/wish-lists/aaaaaaaaaaaaaaaaaaaaaa");
        var malformed = await anonymous.GetAsync("/api/v1/wish-lists/nonsense");

        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);

        var bodies = await Task.WhenAll(
            deleted.Content.ReadAsStringAsync(),
            unknown.Content.ReadAsStringAsync(),
            malformed.Content.ReadAsStringAsync());

        Assert.Single(bodies.Distinct());
    }

    [Fact]
    public async Task Claiming_is_refused_while_maintenance_mode_is_on()
    {
        // FR-025, inherited from the middleware rather than checked in the handler
        // (research R-13) - which is exactly why it is asserted here.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, ("Kuchen", 2));
        var admin = await factory.CreateSignedInClientAsync();

        var switched = await admin.PutAsJsonAsync("/api/v1/admin/maintenance", new { enabled = true });
        switched.EnsureSuccessStatusCode();
        var state = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/maintenance");
        Assert.True(state.GetProperty("enabled").GetBoolean(), "maintenance must be on for this test");

        try
        {
            var anonymous = factory.CreateClient();
            var read = await anonymous.GetAsync($"/api/v1/wish-lists/{shared.ListToken}");
            var claim = await ClaimAsync(anonymous, shared.ListToken, "Anna", shared.Items["Kuchen"]);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, read.StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, claim.StatusCode);
            Assert.Equal("maintenance", await CodeAsync(claim));

            // Nothing was stored while the notice was shown. Scoped to this list: the class
            // shares one storage directory, so other tests have left claims of their own.
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
            Assert.False(await db.WishClaims.AnyAsync(c => c.WishItem!.WishListId == shared.ListId));
        }
        finally
        {
            await admin.PutAsJsonAsync("/api/v1/admin/maintenance", new { enabled = false });
        }
    }

    [Fact]
    public async Task The_personal_link_covers_its_own_entries_and_nothing_else()
    {
        // FR-022c.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, ("Kuchen", 2), ("Grill", null));
        var anonymous = factory.CreateClient();

        var mine = await (await ClaimAsync(anonymous, shared.ListToken, "Anna", shared.Items["Kuchen"]))
            .Content.ReadFromJsonAsync<JsonElement>();
        var theirs = await (await ClaimAsync(anonymous, shared.ListToken, "Ben", shared.Items["Grill"]))
            .Content.ReadFromJsonAsync<JsonElement>();

        var myToken = mine.GetProperty("claimToken").GetString()!;
        var theirClaimId = (await anonymous.GetFromJsonAsync<JsonElement>(
                $"/api/v1/claims/{theirs.GetProperty("claimToken").GetString()}"))
            .GetProperty("entries").EnumerateArray().First().GetProperty("claimId").GetGuid();

        var covered = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/claims/{myToken}");
        Assert.Equal(1, covered.GetProperty("entries").GetArrayLength());
        Assert.Equal("Anna", covered.GetProperty("entries").EnumerateArray().First()
            .GetProperty("displayName").GetString());

        // My token must not reach somebody else's entry.
        var trespass = await anonymous.DeleteAsync($"/api/v1/claims/{myToken}/{theirClaimId}");
        Assert.Equal(HttpStatusCode.NotFound, trespass.StatusCode);
    }

    [Fact]
    public async Task Withdrawing_opens_the_place_again_immediately()
    {
        // FR-022a.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, ("Kuchen", 1));
        var anonymous = factory.CreateClient();

        var claimed = await (await ClaimAsync(anonymous, shared.ListToken, "Anna", shared.Items["Kuchen"]))
            .Content.ReadFromJsonAsync<JsonElement>();
        var token = claimed.GetProperty("claimToken").GetString()!;
        var claimId = (await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/claims/{token}"))
            .GetProperty("entries").EnumerateArray().First().GetProperty("claimId").GetGuid();

        // Full before.
        Assert.Equal(HttpStatusCode.Conflict,
            (await ClaimAsync(anonymous, shared.ListToken, "Ben", shared.Items["Kuchen"])).StatusCode);

        var withdrawn = await anonymous.DeleteAsync($"/api/v1/claims/{token}/{claimId}");
        Assert.Equal(HttpStatusCode.NoContent, withdrawn.StatusCode);

        // Claimable again afterwards.
        Assert.Equal(HttpStatusCode.Created,
            (await ClaimAsync(anonymous, shared.ListToken, "Ben", shared.Items["Kuchen"])).StatusCode);
    }

    [Fact]
    public async Task Withdrawing_twice_says_the_entry_is_gone_rather_than_failing()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, ("Kuchen", 2));
        var anonymous = factory.CreateClient();

        var claimed = await (await ClaimAsync(anonymous, shared.ListToken, "Anna", shared.Items["Kuchen"]))
            .Content.ReadFromJsonAsync<JsonElement>();
        var token = claimed.GetProperty("claimToken").GetString()!;
        var claimId = (await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/claims/{token}"))
            .GetProperty("entries").EnumerateArray().First().GetProperty("claimId").GetGuid();

        await anonymous.DeleteAsync($"/api/v1/claims/{token}/{claimId}");
        var again = await anonymous.DeleteAsync($"/api/v1/claims/{token}/{claimId}");

        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task A_personal_link_dies_with_the_list()
    {
        // FR-022e.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, ("Kuchen", 2));
        var anonymous = factory.CreateClient();

        var claimed = await (await ClaimAsync(anonymous, shared.ListToken, "Anna", shared.Items["Kuchen"]))
            .Content.ReadFromJsonAsync<JsonElement>();
        var token = claimed.GetProperty("claimToken").GetString()!;

        var admin = await factory.CreateSignedInClientAsync();
        await admin.DeleteAsync($"/api/v1/admin/wish-lists/{shared.ListId}");

        var after = await anonymous.GetAsync($"/api/v1/claims/{token}");
        var unknown = await anonymous.GetAsync("/api/v1/claims/aaaaaaaaaaaaaaaaaaaaaa");

        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
        Assert.Equal(
            await unknown.Content.ReadAsStringAsync(),
            await after.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task One_submission_costs_one_of_the_ten_however_many_items_it_carries()
    {
        // FR-023 and SC-002a: counting names would punish exactly the path FR-016 invites.
        using var factory = new ApiFactory(storage.DataDirectory, submissionsPerHour: 10);
        var items = Enumerable.Range(0, 10).Select(i => ($"Sache {i}", (int?)5)).ToArray();
        var shared = await ShareAsync(factory, items);
        var anonymous = factory.CreateClient();

        var all = shared.Items.Values.ToArray();

        // Ten items in one submission: one permit.
        var bulk = await ClaimAsync(anonymous, shared.ListToken, "Anna", all);
        Assert.Equal(HttpStatusCode.Created, bulk.StatusCode);

        // Nine more submissions are inside the budget, the eleventh is not. Each takes a
        // different item, so capacity cannot be what refuses them - the budget must be.
        for (var i = 0; i < 9; i++)
        {
            var response = await ClaimAsync(anonymous, shared.ListToken, $"Gast {i}", all[i]);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var refused = await ClaimAsync(anonymous, shared.ListToken, "Zuspät", all[9]);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("too_many_requests", await CodeAsync(refused));
    }

    [Fact]
    public async Task A_withdrawal_costs_one_of_the_same_ten_a_submission_does()
    {
        // FR-023a. Without this the budget is a door with a window beside it: claim, withdraw,
        // claim again would cost one permit per pair instead of two, and the churn a rate limit
        // exists to bound would be free.
        using var factory = new ApiFactory(storage.DataDirectory, submissionsPerHour: 2);
        var shared = await ShareAsync(factory, ("Kuchen", 5));
        var anonymous = factory.CreateClient();
        var item = shared.Items["Kuchen"];

        // Permit one of two.
        var first = await ClaimAsync(anonymous, shared.ListToken, "Anna", item);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var accepted = await first.Content.ReadFromJsonAsync<JsonElement>();
        var claimToken = accepted.GetProperty("claimToken").GetString()!;

        var covered = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/claims/{claimToken}");
        var claimId = covered.GetProperty("entries").EnumerateArray()
            .First().GetProperty("claimId").GetGuid();

        // Permit two of two: the withdrawal spends from the same budget, not from one of its own.
        var withdrawn = await anonymous.DeleteAsync($"/api/v1/claims/{claimToken}/{claimId}");
        Assert.Equal(HttpStatusCode.NoContent, withdrawn.StatusCode);

        // The item has five places and only ever held one name, so capacity cannot be what
        // refuses this - the budget must be.
        var refused = await ClaimAsync(anonymous, shared.ListToken, "Anna", item);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("too_many_requests", await CodeAsync(refused));
    }
}
