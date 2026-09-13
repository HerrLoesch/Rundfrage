using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Http;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// User Story 1 from the operator's side: creating a wish list, and reading the list of them.
/// </summary>
public class WishListAdminTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private static object ListBody(
        string title = "Sommerfest",
        string? description = "Bitte eintragen",
        string targetDate = "2099-07-18",
        object[]? items = null) => new
        {
            title,
            description,
            targetDate,
            items = items ?? [new { name = "Kuchen", wantedCount = 2 }],
        };

    private static async Task<JsonElement> CreateAsync(HttpClient admin, object body)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/admin/wish-lists", body);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<string> RefusalCodeAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task Creating_a_wish_list_stores_it_and_returns_a_participant_token()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var created = await CreateAsync(admin, ListBody());

        Assert.Equal("Sommerfest", created.GetProperty("title").GetString());
        Assert.Equal("2099-07-18", created.GetProperty("targetDate").GetString());
        Assert.False(created.GetProperty("closed").GetBoolean());
        Assert.Equal(
            Rundfrage.Api.Security.CapabilityToken.TokenLength,
            created.GetProperty("listToken").GetString()!.Length);
    }

    [Fact]
    public async Task An_item_with_no_stated_number_is_wanted_exactly_once()
    {
        // FR-006. The default is the server's, not the form's.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var created = await CreateAsync(admin, ListBody(items:
        [
            new { name = "Grill" },
            new { name = "Kuchen", wantedCount = 3 },
        ]));

        var items = created.GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(1, items.Single(i => i.GetProperty("name").GetString() == "Grill")
            .GetProperty("wantedCount").GetInt32());
        Assert.Equal(3, items.Single(i => i.GetProperty("name").GetString() == "Kuchen")
            .GetProperty("wantedCount").GetInt32());
        Assert.Equal(4, created.GetProperty("placeCount").GetInt32());
    }

    [Fact]
    public async Task Items_keep_the_order_the_operator_entered_them_in()
    {
        // FR-009: the same order for the operator and for every participant.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var created = await CreateAsync(admin, ListBody(items:
        [
            new { name = "Zuletzt" }, new { name = "Mittig" }, new { name = "Anfang" },
        ]));

        Assert.Equal(
            new[] { "Zuletzt", "Mittig", "Anfang" },
            created.GetProperty("items").EnumerateArray()
                .Select(i => i.GetProperty("name").GetString()).ToArray());
    }

    [Theory]
    [InlineData(null, "x", "2026-07-18", ErrorCodes.TitleRequired)]
    [InlineData("Sommerfest", "x", null, ErrorCodes.TargetDateRequired)]
    public async Task A_defective_list_is_refused_naming_the_defect(
        string? title, string? description, string? targetDate, string expected)
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var response = await admin.PostAsJsonAsync("/api/v1/admin/wish-lists", new
        {
            title,
            description,
            targetDate,
            items = new[] { new { name = "Kuchen" } },
        });

        Assert.Equal(expected, await RefusalCodeAsync(response));
    }

    [Fact]
    public async Task A_list_without_items_is_refused()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var response = await admin.PostAsJsonAsync(
            "/api/v1/admin/wish-lists", ListBody(items: []));

        Assert.Equal(ErrorCodes.ItemsRequired, await RefusalCodeAsync(response));
    }

    [Fact]
    public async Task A_duplicate_item_name_is_refused()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var response = await admin.PostAsJsonAsync("/api/v1/admin/wish-lists", ListBody(items:
        [
            new { name = "Kuchen" }, new { name = "kuchen" },
        ]));

        Assert.Equal(ErrorCodes.DuplicateItemName, await RefusalCodeAsync(response));
    }

    [Fact]
    public async Task A_wanted_count_outside_its_range_is_refused_with_its_limit()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var response = await admin.PostAsJsonAsync("/api/v1/admin/wish-lists", ListBody(items:
        [
            new { name = "Kuchen", wantedCount = WishItem.MaxWantedCount + 1 },
        ]));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.WantedCountInvalid, body.GetProperty("code").GetString());
        Assert.Equal(WishItem.MaxWantedCount, body.GetProperty("limit").GetInt32());
    }

    [Fact]
    public async Task More_wished_places_than_the_capacity_are_refused_with_the_headroom()
    {
        // FR-010a: this limit is met where the operator works and never by a participant.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var items = Enumerable.Range(0, 21)
            .Select(i => new { name = $"Sache {i}", wantedCount = WishItem.MaxWantedCount })
            .ToArray<object>();

        var response = await admin.PostAsJsonAsync("/api/v1/admin/wish-lists", ListBody(items: items));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ErrorCodes.TooManyPlaces, body.GetProperty("code").GetString());
        Assert.Equal(WishList.MaxPlaces, body.GetProperty("limit").GetInt32());
    }

    [Fact]
    public async Task A_list_can_be_created_without_a_description()
    {
        // FR-003.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var created = await CreateAsync(admin, ListBody(description: null));

        Assert.True(
            created.TryGetProperty("description", out var description) is false
            || description.ValueKind is JsonValueKind.Null);
    }

    [Fact]
    public async Task Every_wish_list_route_refuses_without_a_session()
    {
        // FR-011, and the refusal reveals nothing about what exists (002 FR-002).
        using var factory = new ApiFactory(storage.DataDirectory);
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/v1/admin/wish-lists")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/v1/admin/wish-lists", ListBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/v1/admin/wish-lists/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task An_unknown_wish_list_answers_the_neutral_payload()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var response = await admin.GetAsync($"/api/v1/admin/wish-lists/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("not_found", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_listing_carries_each_list_with_its_figures()
    {
        // FR-044, FR-045 and FR-049: this is the payload the dashboard reads too, so the two
        // cannot disagree (research R-5). Written before the projection exists.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var created = await CreateAsync(admin, ListBody(title: "Figuren", items:
        [
            new { name = "Kuchen", wantedCount = 2 },
            new { name = "Grill" },
            new { name = "Salat", wantedCount = 3 },
        ]));

        var listToken = created.GetProperty("listToken").GetString()!;
        var anonymous = factory.CreateClient();
        var view = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/wish-lists/{listToken}");
        var kuchenId = view.GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("name").GetString() == "Kuchen").GetProperty("id").GetGuid();

        await anonymous.PostAsJsonAsync($"/api/v1/wish-lists/{listToken}",
            new { displayName = "Anna", itemIds = new[] { kuchenId } });

        var rows = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/wish-lists");
        var row = rows.EnumerateArray().Single(r => r.GetProperty("title").GetString() == "Figuren");

        Assert.Equal(3, row.GetProperty("itemCount").GetInt32());
        Assert.Equal(1, row.GetProperty("entryCount").GetInt32());
        Assert.Equal(6, row.GetProperty("placeCount").GetInt32());
        Assert.Equal(2, row.GetProperty("untakenItemCount").GetInt32());
        Assert.Equal(0, row.GetProperty("completeItemCount").GetInt32());
        Assert.False(row.GetProperty("closed").GetBoolean());
        Assert.Equal(listToken, row.GetProperty("listToken").GetString());
    }

    [Fact]
    public async Task The_listing_puts_open_lists_first_with_the_nearest_target_date_on_top()
    {
        // FR-048c. The order is the API's, so the area and the dashboard agree on it as well.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var far = $"Fern {Guid.NewGuid():N}";
        var near = $"Nah {Guid.NewGuid():N}";
        var closed = $"Zu {Guid.NewGuid():N}";
        var longClosed = $"LangeZu {Guid.NewGuid():N}";

        await CreateAsync(admin, ListBody(title: far, targetDate: "2099-12-31"));
        await CreateAsync(admin, ListBody(title: near, targetDate: "2099-01-01"));
        await CreateAsync(admin, ListBody(title: closed, targetDate: "2020-06-01"));
        await CreateAsync(admin, ListBody(title: longClosed, targetDate: "2019-06-01"));

        var titles = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/wish-lists"))
            .EnumerateArray()
            .Select(r => r.GetProperty("title").GetString()!)
            .Where(t => t == far || t == near || t == closed || t == longClosed)
            .ToArray();

        Assert.Equal(new[] { near, far, closed, longClosed }, titles);
    }
}
