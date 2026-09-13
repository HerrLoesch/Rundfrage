using System.Net;
using System.Text;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rundfrage.Api.Data;
using Rundfrage.Api.Http;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// User Story 2: changing a wish list after it has been shared, and deleting it (008 FR-029 to
/// FR-039). The link keeps working and the names already entered stay where they are.
/// </summary>
public class WishListEditTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private sealed record Shared(
        HttpClient Admin, Guid ListId, string ListToken, Dictionary<string, Guid> Items);

    private static async Task<Shared> ShareAsync(
        ApiFactory factory, string targetDate = "2099-07-18",
        params (string Name, int? Wanted)[] items)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var response = await admin.PostAsJsonAsync("/api/v1/admin/wish-lists", new
        {
            title = "Sommerfest",
            description = "Bitte eintragen",
            targetDate,
            items = items.Length == 0
                ? [new { name = "Kuchen", wantedCount = (int?)2 }]
                : items.Select(i => new { name = i.Name, wantedCount = i.Wanted }).ToArray(),
        });

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();

        return new Shared(
            admin,
            created.GetProperty("id").GetGuid(),
            created.GetProperty("listToken").GetString()!,
            created.GetProperty("items").EnumerateArray().ToDictionary(
                i => i.GetProperty("name").GetString()!, i => i.GetProperty("id").GetGuid()));
    }

    private static async Task ClaimAsync(ApiFactory factory, string listToken, string name, Guid itemId)
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            $"/api/v1/wish-lists/{listToken}", new { displayName = name, itemIds = new[] { itemId } });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task The_title_description_and_target_date_can_be_changed()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory);

        var patched = await shared.Admin.PatchAsJsonAsync(
            $"/api/v1/admin/wish-lists/{shared.ListId}",
            new { title = "Herbstfest", description = "Neu", targetDate = "2099-10-02" });

        patched.EnsureSuccessStatusCode();
        var body = await patched.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Herbstfest", body.GetProperty("title").GetString());
        Assert.Equal("Neu", body.GetProperty("description").GetString());
        Assert.Equal("2099-10-02", body.GetProperty("targetDate").GetString());

        // FR-035: the participant link is unchanged, and still works.
        Assert.Equal(shared.ListToken, body.GetProperty("listToken").GetString());
        var view = await factory.CreateClient()
            .GetFromJsonAsync<JsonElement>($"/api/v1/wish-lists/{shared.ListToken}");
        Assert.Equal("Herbstfest", view.GetProperty("title").GetString());
    }

    [Fact]
    public async Task An_item_can_be_added_without_touching_the_others()
    {
        // FR-030.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, items: [("Kuchen", 2)]);
        await ClaimAsync(factory, shared.ListToken, "Anna", shared.Items["Kuchen"]);

        var added = await shared.Admin.PostAsJsonAsync(
            $"/api/v1/admin/wish-lists/{shared.ListId}/items", new { name = "Salat", wantedCount = 3 });

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var body = await added.Content.ReadFromJsonAsync<JsonElement>();

        var items = body.GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(new[] { "Kuchen", "Salat" },
            items.Select(i => i.GetProperty("name").GetString()));
        Assert.Equal(1, items[0].GetProperty("claims").GetArrayLength());
        Assert.Equal(0, items[1].GetProperty("claims").GetArrayLength());
        Assert.Equal(5, body.GetProperty("placeCount").GetInt32());
    }

    [Fact]
    public async Task Renaming_an_item_keeps_its_claims()
    {
        // FR-031: the same item under a new name, not a new item.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, items: [("Getränke", 2)]);
        await ClaimAsync(factory, shared.ListToken, "Anna", shared.Items["Getränke"]);

        var renamed = await shared.Admin.PatchAsJsonAsync(
            $"/api/v1/admin/wish-lists/{shared.ListId}/items/{shared.Items["Getränke"]}",
            new { name = "Alkoholfreie Getränke" });

        renamed.EnsureSuccessStatusCode();
        var item = (await renamed.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().First();

        Assert.Equal("Alkoholfreie Getränke", item.GetProperty("name").GetString());
        Assert.Equal("Anna", item.GetProperty("claims").EnumerateArray().First()
            .GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Raising_a_wanted_count_opens_places_and_keeps_the_claims()
    {
        // FR-032.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, items: [("Kuchen", 2)]);
        await ClaimAsync(factory, shared.ListToken, "Anna", shared.Items["Kuchen"]);
        await ClaimAsync(factory, shared.ListToken, "Ben", shared.Items["Kuchen"]);

        var raised = await shared.Admin.PatchAsJsonAsync(
            $"/api/v1/admin/wish-lists/{shared.ListId}/items/{shared.Items["Kuchen"]}",
            new { wantedCount = 4 });

        raised.EnsureSuccessStatusCode();

        var view = await factory.CreateClient()
            .GetFromJsonAsync<JsonElement>($"/api/v1/wish-lists/{shared.ListToken}");
        var item = view.GetProperty("items").EnumerateArray().First();

        Assert.Equal(4, item.GetProperty("wantedCount").GetInt32());
        Assert.Equal(2, item.GetProperty("openPlaces").GetInt32());
        Assert.Equal(2, item.GetProperty("names").GetArrayLength());
    }

    [Fact]
    public async Task Lowering_a_count_below_the_claims_made_is_refused_with_that_number()
    {
        // FR-033: the refusal names what the operator has to deal with first.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, items: [("Kuchen", 4)]);
        await ClaimAsync(factory, shared.ListToken, "Anna", shared.Items["Kuchen"]);
        await ClaimAsync(factory, shared.ListToken, "Ben", shared.Items["Kuchen"]);
        await ClaimAsync(factory, shared.ListToken, "Chris", shared.Items["Kuchen"]);

        var lowered = await shared.Admin.PatchAsJsonAsync(
            $"/api/v1/admin/wish-lists/{shared.ListId}/items/{shared.Items["Kuchen"]}",
            new { wantedCount = 2 });

        Assert.Equal(HttpStatusCode.BadRequest, lowered.StatusCode);
        var body = await lowered.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ErrorCodes.CountBelowEntries, body.GetProperty("code").GetString());
        Assert.Equal(3, body.GetProperty("limit").GetInt32());

        // Lowering to exactly that number is allowed, and leaves the item complete.
        var exact = await shared.Admin.PatchAsJsonAsync(
            $"/api/v1/admin/wish-lists/{shared.ListId}/items/{shared.Items["Kuchen"]}",
            new { wantedCount = 3 });

        exact.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Renaming_into_an_existing_name_is_refused()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, items: [("Kuchen", 2), ("Salat", 2)]);

        var response = await shared.Admin.PatchAsJsonAsync(
            $"/api/v1/admin/wish-lists/{shared.ListId}/items/{shared.Items["Salat"]}",
            new { name = "Kuchen" });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ErrorCodes.DuplicateItemName, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Removing_an_item_destroys_exactly_its_claims()
    {
        // FR-034.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, items: [("Kuchen", 2), ("Salat", 2)]);
        await ClaimAsync(factory, shared.ListToken, "Anna", shared.Items["Kuchen"]);
        await ClaimAsync(factory, shared.ListToken, "Ben", shared.Items["Salat"]);

        var removed = await shared.Admin.DeleteAsync(
            $"/api/v1/admin/wish-lists/{shared.ListId}/items/{shared.Items["Kuchen"]}");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
        Assert.False(await db.WishClaims.AnyAsync(c => c.WishItemId == shared.Items["Kuchen"]));
        Assert.True(await db.WishClaims.AnyAsync(c => c.WishItemId == shared.Items["Salat"]));
    }

    [Fact]
    public async Task The_operator_can_delete_one_claim_without_touching_the_rest()
    {
        // FR-043a, and the place is open again afterwards.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, items: [("Kuchen", 2)]);
        await ClaimAsync(factory, shared.ListToken, "Anna", shared.Items["Kuchen"]);
        await ClaimAsync(factory, shared.ListToken, "Ben", shared.Items["Kuchen"]);

        var detail = await shared.Admin.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/wish-lists/{shared.ListId}");
        var annasClaim = detail.GetProperty("items").EnumerateArray().First()
            .GetProperty("claims").EnumerateArray()
            .First(c => c.GetProperty("displayName").GetString() == "Anna")
            .GetProperty("id").GetGuid();

        var deleted = await shared.Admin.DeleteAsync(
            $"/api/v1/admin/wish-lists/{shared.ListId}/claims/{annasClaim}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var view = await factory.CreateClient()
            .GetFromJsonAsync<JsonElement>($"/api/v1/wish-lists/{shared.ListToken}");
        var item = view.GetProperty("items").EnumerateArray().First();

        Assert.Equal(1, item.GetProperty("openPlaces").GetInt32());
        Assert.Equal(new[] { "Ben" },
            item.GetProperty("names").EnumerateArray().Select(n => n.GetString()));
    }

    [Fact]
    public async Task Deleting_the_list_removes_everything_beneath_it()
    {
        // FR-037 to FR-039, checked in storage: deletion removes rows rather than hiding them.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, items: [("Kuchen", 2)]);
        await ClaimAsync(factory, shared.ListToken, "Anna", shared.Items["Kuchen"]);

        var deleted = await shared.Admin.DeleteAsync($"/api/v1/admin/wish-lists/{shared.ListId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();

        Assert.False(await db.WishLists.AnyAsync(l => l.Id == shared.ListId));
        Assert.False(await db.WishItems.AnyAsync(i => i.WishListId == shared.ListId));
        Assert.False(await db.WishClaims.AnyAsync(c => c.WishItemId == shared.Items["Kuchen"]));
    }

    [Fact]
    public async Task Every_figure_follows_every_change_that_counts()
    {
        // SC-004: the figures match a hand count after each of the five things that move them.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, items: [("Kuchen", 2), ("Grill", 1), ("Salat", 3)]);

        async Task<(int Entries, int Places, int Untaken, int Complete)> FiguresAsync()
        {
            var rows = await shared.Admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/wish-lists");
            var row = rows.EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == shared.ListId);

            return (
                row.GetProperty("entryCount").GetInt32(),
                row.GetProperty("placeCount").GetInt32(),
                row.GetProperty("untakenItemCount").GetInt32(),
                row.GetProperty("completeItemCount").GetInt32());
        }

        // Nothing claimed: six places, three items untaken, none complete.
        Assert.Equal((0, 6, 3, 0), await FiguresAsync());

        // A claim.
        await ClaimAsync(factory, shared.ListToken, "Anna", shared.Items["Grill"]);
        Assert.Equal((1, 6, 2, 1), await FiguresAsync());

        // A withdrawal through the personal link.
        var anonymous = factory.CreateClient();
        var claimed = await anonymous.PostAsJsonAsync($"/api/v1/wish-lists/{shared.ListToken}",
            new { displayName = "Ben", itemIds = new[] { shared.Items["Kuchen"] } });
        var token = (await claimed.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("claimToken").GetString()!;
        var claimId = (await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/claims/{token}"))
            .GetProperty("entries").EnumerateArray().First().GetProperty("claimId").GetGuid();

        Assert.Equal((2, 6, 1, 1), await FiguresAsync());
        await anonymous.DeleteAsync($"/api/v1/claims/{token}/{claimId}");
        Assert.Equal((1, 6, 2, 1), await FiguresAsync());

        // A raised wanted count: places grow, and the complete item is complete no longer.
        await shared.Admin.PatchAsJsonAsync(
            $"/api/v1/admin/wish-lists/{shared.ListId}/items/{shared.Items["Grill"]}",
            new { wantedCount = 2 });
        Assert.Equal((1, 7, 2, 0), await FiguresAsync());

        // An item removed with its claims.
        await shared.Admin.DeleteAsync(
            $"/api/v1/admin/wish-lists/{shared.ListId}/items/{shared.Items["Grill"]}");
        Assert.Equal((0, 5, 2, 0), await FiguresAsync());

        // And an operator deleting one entry.
        await ClaimAsync(factory, shared.ListToken, "Chris", shared.Items["Salat"]);
        var detail = await shared.Admin.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/wish-lists/{shared.ListId}");
        var chrissClaim = detail.GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("name").GetString() == "Salat")
            .GetProperty("claims").EnumerateArray().First().GetProperty("id").GetGuid();

        Assert.Equal((1, 5, 1, 0), await FiguresAsync());
        await shared.Admin.DeleteAsync(
            $"/api/v1/admin/wish-lists/{shared.ListId}/claims/{chrissClaim}");
        Assert.Equal((0, 5, 2, 0), await FiguresAsync());
    }

    [Fact]
    public async Task The_detail_and_the_listing_agree_about_one_list()
    {
        // FR-049 in the small: both come from one projection, and this is what would fail if a
        // second one were ever introduced for the dashboard (research R-5).
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, items: [("Kuchen", 2), ("Grill", 1)]);
        await ClaimAsync(factory, shared.ListToken, "Anna", shared.Items["Kuchen"]);

        var detail = await shared.Admin.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/wish-lists/{shared.ListId}");
        var row = (await shared.Admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/wish-lists"))
            .EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == shared.ListId);

        Assert.Equal(detail.GetProperty("entryCount").GetInt32(), row.GetProperty("entryCount").GetInt32());
        Assert.Equal(detail.GetProperty("placeCount").GetInt32(), row.GetProperty("placeCount").GetInt32());
        Assert.Equal(detail.GetProperty("closed").GetBoolean(), row.GetProperty("closed").GetBoolean());
        Assert.Equal(detail.GetProperty("title").GetString(), row.GetProperty("title").GetString());
    }

    // --- The closed state (FR-028a to FR-028e) ---------------------------------------------

    [Fact]
    public async Task A_list_whose_target_day_has_passed_is_closed_but_entirely_present()
    {
        // FR-028, FR-028b: closing is not hiding and it is certainly not deleting.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, targetDate: "2020-06-01", items: [("Kuchen", 2)]);

        var view = await factory.CreateClient()
            .GetFromJsonAsync<JsonElement>($"/api/v1/wish-lists/{shared.ListToken}");

        Assert.True(view.GetProperty("closed").GetBoolean());
        Assert.Equal("Sommerfest", view.GetProperty("title").GetString());
        Assert.Equal(1, view.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task A_closed_list_refuses_a_claim_and_a_withdrawal()
    {
        // FR-028a and FR-028d, both judged on arrival (FR-028e).
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, items: [("Kuchen", 2)]);
        await ClaimAsync(factory, shared.ListToken, "Anna", shared.Items["Kuchen"]);

        var anonymous = factory.CreateClient();
        var claimed = await anonymous.PostAsJsonAsync($"/api/v1/wish-lists/{shared.ListToken}",
            new { displayName = "Ben", itemIds = new[] { shared.Items["Kuchen"] } });
        var token = (await claimed.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("claimToken").GetString()!;
        var claimId = (await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/claims/{token}"))
            .GetProperty("entries").EnumerateArray().First().GetProperty("claimId").GetGuid();

        // The occasion moves into the past.
        await shared.Admin.PatchAsJsonAsync(
            $"/api/v1/admin/wish-lists/{shared.ListId}", new { targetDate = "2020-06-01" });

        var lateClaim = await anonymous.PostAsJsonAsync($"/api/v1/wish-lists/{shared.ListToken}",
            new { displayName = "Chris", itemIds = new[] { shared.Items["Kuchen"] } });
        var lateWithdrawal = await anonymous.DeleteAsync($"/api/v1/claims/{token}/{claimId}");

        Assert.Equal(HttpStatusCode.BadRequest, lateClaim.StatusCode);
        Assert.Equal(ErrorCodes.WishListClosed,
            (await lateClaim.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, lateWithdrawal.StatusCode);
        Assert.Equal(ErrorCodes.WishListClosed,
            (await lateWithdrawal.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        // The operator can still remove an entry, whatever the clock says.
        var byOperator = await shared.Admin.DeleteAsync(
            $"/api/v1/admin/wish-lists/{shared.ListId}/claims/{claimId}");
        Assert.Equal(HttpStatusCode.NoContent, byOperator.StatusCode);
    }

    [Fact]
    public async Task Moving_the_target_date_forward_reopens_a_closed_list()
    {
        // FR-028c: nothing is stored, so there is no state to repair.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, targetDate: "2020-06-01", items: [("Kuchen", 2)]);

        await shared.Admin.PatchAsJsonAsync(
            $"/api/v1/admin/wish-lists/{shared.ListId}", new { targetDate = "2099-06-01" });

        var anonymous = factory.CreateClient();
        var view = await anonymous.GetFromJsonAsync<JsonElement>(
            $"/api/v1/wish-lists/{shared.ListToken}");
        var claimed = await anonymous.PostAsJsonAsync($"/api/v1/wish-lists/{shared.ListToken}",
            new { displayName = "Anna", itemIds = new[] { shared.Items["Kuchen"] } });

        Assert.False(view.GetProperty("closed").GetBoolean());
        Assert.Equal(HttpStatusCode.Created, claimed.StatusCode);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("5")]
    [InlineData("\"gestern\"")]
    [InlineData("{\"targetDate\":\"gestern\"}")]
    public async Task A_patch_that_is_not_a_change_is_refused_rather_than_crashing(string body)
    {
        // The patch route reads the raw document, because an absent description and a cleared one
        // are different instructions. That reading throws on anything that is not an object, and
        // an unhandled throw is a 500 - which tells the caller nothing and the log something
        // alarming that is not true.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory);

        var response = await shared.Admin.PatchAsync(
            $"/api/v1/admin/wish-lists/{shared.ListId}",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var refusal = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ErrorCodes.MalformedRequest, refusal.GetProperty("code").GetString());

        // And it changed nothing.
        var after = await shared.Admin.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/wish-lists/{shared.ListId}");
        Assert.Equal("Sommerfest", after.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("kuchen")]
    [InlineData("KUCHEN")]
    [InlineData("  Kuchen  ")]
    public async Task A_name_that_differs_only_in_case_or_padding_is_the_same_name(string name)
    {
        // FR-008. The service compares with OrdinalIgnoreCase after trimming, and the unique
        // index carries NOCASE so it agrees rather than quietly permitting what the service
        // refuses - which would leave a second write path able to create the duplicate.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, items: [("Kuchen", 2)]);

        var added = await shared.Admin.PostAsJsonAsync(
            $"/api/v1/admin/wish-lists/{shared.ListId}/items", new { name, wantedCount = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, added.StatusCode);

        var refusal = await added.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ErrorCodes.DuplicateItemName, refusal.GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_unique_index_refuses_the_duplicate_the_service_never_sees()
    {
        // The index is not decoration: it is what holds when a write reaches the table without
        // passing WishListService. Asserted against the database directly, because no route can
        // produce this - which is exactly why nothing would notice the index going missing.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory, items: [("Kuchen", 2)]);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();

        db.WishItems.Add(new Rundfrage.Api.Data.Entities.WishItem
        {
            Id = Guid.CreateVersion7(),
            WishListId = shared.ListId,
            Name = "kuchen",
            WantedCount = 1,
            Position = 1,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task An_empty_patch_is_accepted_and_leaves_the_list_as_it_was()
    {
        // The other side of the theory above: {} *is* an object, and "change nothing" is a
        // legitimate instruction rather than a defect.
        using var factory = new ApiFactory(storage.DataDirectory);
        var shared = await ShareAsync(factory);

        var response = await shared.Admin.PatchAsync(
            $"/api/v1/admin/wish-lists/{shared.ListId}",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Sommerfest", after.GetProperty("title").GetString());
        Assert.Equal("Bitte eintragen", after.GetProperty("description").GetString());
        Assert.Equal("2099-07-18", after.GetProperty("targetDate").GetString());
    }
}
