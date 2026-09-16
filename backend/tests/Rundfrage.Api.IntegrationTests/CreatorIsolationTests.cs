using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 009 US2: the suite that defines the feature (FR-032 to FR-035, SC-002, SC-003).
/// </summary>
/// <remarks>
/// <b>The same promise <c>OwnerScopeTests</c> proved at the filter, now proved through HTTP on
/// every route.</b> Both are needed and neither replaces the other: the filter test says the
/// queryable is right, this says every handler actually goes through it.
/// <para>
/// The table below is deliberately exhaustive over the creator surface. A route added later
/// without a case here is visible as a route nobody checked - which is the failure this feature
/// most needs to avoid, because a leak looks exactly like a working feature.
/// </para>
/// </remarks>
public sealed class CreatorIsolationTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private ApiFactory NewFactory() => new(storage.DataDirectory, creatorWritesPerHour: 1000);

    private sealed record Stage(
        HttpClient Anna, string AnnaToken,
        HttpClient Ben, string BenToken,
        Guid BensPoll, Guid BensList, Guid BensItem, Guid OperatorsPoll, Guid OperatorsList);

    private static async Task<Stage> StageAsync(ApiFactory factory)
    {
        var (_, annaToken) = await CreatorTestData.NewCreatorAsync(factory, $"Anna {Guid.NewGuid():n}");
        var (_, benToken) = await CreatorTestData.NewCreatorAsync(factory, $"Ben {Guid.NewGuid():n}");

        var anna = factory.CreateClient();
        var ben = factory.CreateClient();

        await CreatorTestData.NewPollAsync(anna, annaToken, "Annas Termin");
        await CreatorTestData.NewWishListAsync(anna, annaToken, "Annas Liste");

        var bensPoll = await CreatorTestData.NewPollAsync(ben, benToken, "Bens Termin");
        var bensList = await CreatorTestData.NewWishListAsync(ben, benToken, "Bens Liste");

        var detail = await ben.GetFromJsonAsync<JsonElement>(
            $"/api/v1/e/{benToken}/wish-lists/{bensList}");
        var bensItem = detail.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();

        // And something owned by the operator, whose ownership is a null CreatorId.
        var admin = await factory.CreateSignedInClientAsync();
        var operatorsPoll = (await (await admin.PostAsJsonAsync("/api/v1/admin/polls", new
        {
            title = "Termin des Betreibers",
            days = new[] { "2026-12-05" },
        })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var operatorsList = (await (await admin.PostAsJsonAsync("/api/v1/admin/wish-lists", new
        {
            title = "Liste des Betreibers",
            targetDate = "2026-12-24",
            items = new[] { new { name = "Kerzen", wantedCount = 1 } },
        })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        return new Stage(anna, annaToken, ben, benToken,
            bensPoll, bensList, bensItem, operatorsPoll, operatorsList);
    }

    [Fact]
    public async Task Each_link_lists_only_its_own_and_indicates_nothing_else_exists()
    {
        using var factory = NewFactory();
        var stage = await StageAsync(factory);

        var surface = await stage.Anna.GetFromJsonAsync<JsonElement>($"/api/v1/e/{stage.AnnaToken}");

        var pollTitles = surface.GetProperty("polls").EnumerateArray()
            .Select(p => p.GetProperty("title").GetString()).ToArray();
        var listTitles = surface.GetProperty("wishLists").EnumerateArray()
            .Select(l => l.GetProperty("title").GetString()).ToArray();

        Assert.Equal(new[] { "Annas Termin" }, pollTitles);
        Assert.Equal(new[] { "Annas Liste" }, listTitles);

        // FR-032: not in a list, not in a count, and not indicated to exist. The whole response is
        // checked as text, so a stray identifier anywhere in it fails - including in a field
        // somebody adds later without thinking about this.
        var raw = await stage.Anna.GetStringAsync($"/api/v1/e/{stage.AnnaToken}");

        Assert.DoesNotContain("Bens", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("Betreibers", raw, StringComparison.Ordinal);
        Assert.DoesNotContain(stage.BensPoll.ToString(), raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(stage.OperatorsPoll.ToString(), raw, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Every route of the creator surface that names something, aimed at content Anna does not own.
    /// </summary>
    /// <remarks>
    /// Exhaustive by intent. If a route is added to <c>CreatorEndpoints</c> and not to this list,
    /// the omission is the finding.
    /// </remarks>
    public static TheoryData<string, string> CrossOwnerRoutes()
    {
        var data = new TheoryData<string, string>
        {
            { "GET", "polls/{bensPoll}" },
            { "GET", "polls/{bensPoll}/export" },
            { "DELETE", "polls/{bensPoll}" },
            { "DELETE", "polls/{bensPoll}/responses/{someGuid}" },
            { "GET", "wish-lists/{bensList}" },
            { "PATCH", "wish-lists/{bensList}" },
            { "DELETE", "wish-lists/{bensList}" },
            { "POST", "wish-lists/{bensList}/items" },
            { "PATCH", "wish-lists/{bensList}/items/{bensItem}" },
            { "DELETE", "wish-lists/{bensList}/items/{bensItem}" },
            { "DELETE", "wish-lists/{bensList}/claims/{someGuid}" },
            { "GET", "polls/{operatorsPoll}" },
            { "GET", "polls/{operatorsPoll}/export" },
            { "DELETE", "polls/{operatorsPoll}" },
            { "GET", "wish-lists/{operatorsList}" },
            { "PATCH", "wish-lists/{operatorsList}" },
            { "DELETE", "wish-lists/{operatorsList}" },
        };

        return data;
    }

    [Theory]
    [MemberData(nameof(CrossOwnerRoutes))]
    public async Task Another_owners_identifier_answers_exactly_what_an_invented_one_answers(
        string method, string template)
    {
        using var factory = NewFactory();
        var stage = await StageAsync(factory);

        var someGuid = Guid.CreateVersion7();

        string Fill(string path, bool invented) => path
            .Replace("{bensPoll}", (invented ? Guid.CreateVersion7() : stage.BensPoll).ToString())
            .Replace("{bensList}", (invented ? Guid.CreateVersion7() : stage.BensList).ToString())
            .Replace("{bensItem}", (invented ? Guid.CreateVersion7() : stage.BensItem).ToString())
            .Replace("{operatorsPoll}", (invented ? Guid.CreateVersion7() : stage.OperatorsPoll).ToString())
            .Replace("{operatorsList}", (invented ? Guid.CreateVersion7() : stage.OperatorsList).ToString())
            .Replace("{someGuid}", someGuid.ToString());

        async Task<(HttpStatusCode Status, string Body)> CallAsync(bool invented)
        {
            var url = $"/api/v1/e/{stage.AnnaToken}/{Fill(template, invented)}";

            var request = new HttpRequestMessage(new HttpMethod(method), url);
            if (method is "POST" or "PATCH")
            {
                request.Content = JsonContent.Create(new { name = "Irgendwas", title = "Irgendwas" });
            }

            var response = await stage.Anna.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        var notMine = await CallAsync(invented: false);
        var neverExisted = await CallAsync(invented: true);

        // FR-034 and SC-003. Not "both are errors" - byte-identical, so an observer cannot tell
        // "belongs to somebody else" from "no such thing". A 403 here would confirm existence.
        Assert.Equal(HttpStatusCode.NotFound, notMine.Status);
        Assert.Equal(neverExisted.Status, notMine.Status);
        Assert.Equal(neverExisted.Body, notMine.Body);
        Assert.Equal("{\"code\":\"not_found\"}", notMine.Body);
    }

    [Fact]
    public async Task Nothing_belonging_to_another_owner_is_changed_by_an_attempt()
    {
        // The other half of the refusals above: they must also have done nothing. A route that
        // answers 404 after deleting the row would pass every assertion in the theory.
        using var factory = NewFactory();
        var stage = await StageAsync(factory);

        await stage.Anna.DeleteAsync($"/api/v1/e/{stage.AnnaToken}/polls/{stage.BensPoll}");
        await stage.Anna.DeleteAsync($"/api/v1/e/{stage.AnnaToken}/wish-lists/{stage.BensList}");
        await stage.Anna.PatchAsJsonAsync(
            $"/api/v1/e/{stage.AnnaToken}/wish-lists/{stage.BensList}", new { title = "Gekapert" });
        await stage.Anna.DeleteAsync(
            $"/api/v1/e/{stage.AnnaToken}/wish-lists/{stage.BensList}/items/{stage.BensItem}");

        var bensSurface = await stage.Ben.GetFromJsonAsync<JsonElement>($"/api/v1/e/{stage.BenToken}");

        Assert.Single(bensSurface.GetProperty("polls").EnumerateArray());
        Assert.Single(bensSurface.GetProperty("wishLists").EnumerateArray());
        Assert.Equal(
            "Bens Liste",
            bensSurface.GetProperty("wishLists").EnumerateArray().First()
                .GetProperty("title").GetString());

        var detail = await stage.Ben.GetFromJsonAsync<JsonElement>(
            $"/api/v1/e/{stage.BenToken}/wish-lists/{stage.BensList}");
        Assert.Single(detail.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task One_link_cannot_be_used_to_reach_content_through_another_links_path()
    {
        // The obvious attempt, spelled out: Anna's identifier in the path, Ben's token in it.
        // Holding a valid token does not make every identifier reachable - the scope is built from
        // the token that arrived, not from the one the caller would prefer.
        using var factory = NewFactory();
        var stage = await StageAsync(factory);

        var response = await stage.Anna.GetAsync(
            $"/api/v1/e/{stage.BenToken}/polls/{stage.BensPoll}");

        // Anna's client holds no session and no cookie, so this is simply Ben's link being used by
        // whoever holds it - which is exactly the bearer-link design, and is allowed. The point of
        // the assertion is the inverse below.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var crossed = await stage.Anna.GetAsync(
            $"/api/v1/e/{stage.AnnaToken}/polls/{stage.BensPoll}");

        Assert.Equal(HttpStatusCode.NotFound, crossed.StatusCode);
    }
}
