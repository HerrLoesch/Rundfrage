using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 009 US1: the whole of what a holder may do with their own content — create, read, edit, delete
/// and export (FR-022 to FR-028).
/// </summary>
/// <remarks>
/// The happy path, deliberately as one suite. <c>CreatorIsolationTests</c> proves that none of
/// these routes reaches another owner; without this, "refuses everything" would pass that suite
/// perfectly.
/// </remarks>
public sealed class CreatorCreationTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private ApiFactory NewFactory() => new(storage.DataDirectory);

    private static async Task<(HttpClient Client, string Token)> AsCreatorAsync(
        ApiFactory factory, string name)
    {
        var (_, token) = await CreatorTestData.NewCreatorAsync(factory, name);
        return (factory.CreateClient(), token);
    }

    [Fact]
    public async Task A_poll_made_through_a_link_belongs_to_that_Ersteller_and_answers_participants()
    {
        using var factory = NewFactory();
        var (client, token) = await AsCreatorAsync(factory, "Poll Anna");

        var pollId = await CreatorTestData.NewPollAsync(client, token);

        // It shows up on the holder's own surface.
        var surface = await client.GetFromJsonAsync<JsonElement>($"/api/v1/e/{token}");
        var polls = surface.GetProperty("polls").EnumerateArray().ToArray();
        Assert.Single(polls);
        Assert.Equal(pollId, polls[0].GetProperty("id").GetGuid());

        // FR-014: the participant link works for somebody holding neither the creator link nor the
        // operator password. An Ersteller's poll is an ordinary poll.
        var participantToken = polls[0].GetProperty("participantToken").GetString();
        var stranger = factory.CreateClient();

        var poll = await stranger.GetAsync($"/api/v1/polls/{participantToken}");

        Assert.Equal(HttpStatusCode.OK, poll.StatusCode);
    }

    [Fact]
    public async Task A_wish_list_made_through_a_link_belongs_to_that_Ersteller_and_answers_participants()
    {
        using var factory = NewFactory();
        var (client, token) = await AsCreatorAsync(factory, "Wish Anna");

        var listId = await CreatorTestData.NewWishListAsync(client, token);

        var surface = await client.GetFromJsonAsync<JsonElement>($"/api/v1/e/{token}");
        var lists = surface.GetProperty("wishLists").EnumerateArray().ToArray();
        Assert.Single(lists);
        Assert.Equal(listId, lists[0].GetProperty("id").GetGuid());

        var listToken = lists[0].GetProperty("listToken").GetString();
        var stranger = factory.CreateClient();

        var list = await stranger.GetAsync($"/api/v1/wish-lists/{listToken}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    [Fact]
    public async Task Every_limit_of_features_002_and_008_applies_unchanged()
    {
        // FR-022 and FR-023: an Ersteller meets exactly the limits the operator meets, refused by
        // the same validation with the same codes. Nothing about this path is more permissive.
        using var factory = NewFactory();
        var (client, token) = await AsCreatorAsync(factory, "Limit Anna");

        var noTitle = await client.PostAsJsonAsync($"/api/v1/e/{token}/polls", new
        {
            title = "",
            days = new[] { "2026-12-01" },
        });
        Assert.Equal(HttpStatusCode.BadRequest, noTitle.StatusCode);
        Assert.Equal(
            "title_required",
            (await noTitle.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        var noDays = await client.PostAsJsonAsync($"/api/v1/e/{token}/polls", new
        {
            title = "Ohne Tage",
            days = Array.Empty<string>(),
        });
        Assert.Equal(HttpStatusCode.BadRequest, noDays.StatusCode);

        var noItems = await client.PostAsJsonAsync($"/api/v1/e/{token}/wish-lists", new
        {
            title = "Ohne Wünsche",
            targetDate = "2026-12-24",
            items = Array.Empty<object>(),
        });
        Assert.Equal(HttpStatusCode.BadRequest, noItems.StatusCode);
        Assert.Equal(
            "items_required",
            (await noItems.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_holder_reads_their_own_results_and_wish_list_detail()
    {
        // FR-024 and FR-025: everything the operator sees for content of their own.
        using var factory = NewFactory();
        var (client, token) = await AsCreatorAsync(factory, "Read Anna");

        var pollId = await CreatorTestData.NewPollAsync(client, token);
        var listId = await CreatorTestData.NewWishListAsync(client, token);

        var results = await client.GetAsync($"/api/v1/e/{token}/polls/{pollId}");
        Assert.Equal(HttpStatusCode.OK, results.StatusCode);

        var detail = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/e/{token}/wish-lists/{listId}");
        Assert.Equal("Sommerfest", detail.GetProperty("title").GetString());
        Assert.Single(detail.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task A_holder_edits_their_own_wish_list_exactly_as_the_operator_could()
    {
        // FR-026, across all of 008 FR-029 to FR-034.
        using var factory = NewFactory();
        var (client, token) = await AsCreatorAsync(factory, "Edit Anna");

        var listId = await CreatorTestData.NewWishListAsync(client, token);
        var basePath = $"/api/v1/e/{token}/wish-lists/{listId}";

        var renamed = await client.PatchAsJsonAsync(basePath, new { title = "Winterfest" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal(
            "Winterfest",
            (await renamed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());

        var added = await client.PostAsJsonAsync(
            $"{basePath}/items", new { name = "Servietten", wantedCount = 1 });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        var items = (await added.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, items.Length);

        var kuchen = items.Single(i => i.GetProperty("name").GetString() == "Kuchen");
        var raised = await client.PatchAsJsonAsync(
            $"{basePath}/items/{kuchen.GetProperty("id").GetGuid()}", new { wantedCount = 4 });
        Assert.Equal(HttpStatusCode.OK, raised.StatusCode);

        var servietten = items.Single(i => i.GetProperty("name").GetString() == "Servietten");
        var removed = await client.DeleteAsync(
            $"{basePath}/items/{servietten.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
    }

    [Fact]
    public async Task A_holder_deletes_their_own_content_and_the_entries_within_it()
    {
        // FR-027.
        using var factory = NewFactory();
        var (client, token) = await AsCreatorAsync(factory, "Delete Anna");

        var pollId = await CreatorTestData.NewPollAsync(client, token);
        var listId = await CreatorTestData.NewWishListAsync(client, token);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/v1/e/{token}/polls/{pollId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/v1/e/{token}/wish-lists/{listId}")).StatusCode);

        var surface = await client.GetFromJsonAsync<JsonElement>($"/api/v1/e/{token}");
        Assert.Empty(surface.GetProperty("polls").EnumerateArray());
        Assert.Empty(surface.GetProperty("wishLists").EnumerateArray());
    }

    [Fact]
    public async Task An_export_is_byte_for_byte_what_the_operator_receives()
    {
        // SC-004a. Not "similar" and not "contains the same answers" - the same bytes, because it
        // is the same builder. A creator-specific export would be a second document format to keep
        // in step with feature 003's.
        using var factory = NewFactory();
        var (client, token) = await AsCreatorAsync(factory, "Export Anna");

        var pollId = await CreatorTestData.NewPollAsync(client, token);

        var asCreator = await client.GetAsync($"/api/v1/e/{token}/polls/{pollId}/export");
        Assert.Equal(HttpStatusCode.OK, asCreator.StatusCode);

        var admin = await factory.CreateSignedInClientAsync();
        var asOperator = await admin.GetAsync($"/api/v1/admin/polls/{pollId}/export");
        Assert.Equal(HttpStatusCode.OK, asOperator.StatusCode);

        // The exported-at timestamp is the one field that legitimately differs between two calls,
        // so it is normalised away rather than the comparison being weakened to "roughly equal".
        static string WithoutTimestamp(string json) =>
            System.Text.RegularExpressions.Regex.Replace(
                json, "\"exportedAt\":\"[^\"]+\"", "\"exportedAt\":\"-\"");

        Assert.Equal(
            WithoutTimestamp(await asOperator.Content.ReadAsStringAsync()),
            WithoutTimestamp(await asCreator.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task No_route_reachable_with_a_creator_token_accepts_an_uploaded_file()
    {
        // FR-028a and SC-004. Import would put feature 005's upload-and-parse path behind the
        // weakest credential in the system, in exchange for a capability nobody asked for.
        using var factory = NewFactory();
        var (client, token) = await AsCreatorAsync(factory, "Upload Anna");

        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent("{}"u8.ToArray()), "file", "poll.json" },
        };

        foreach (var path in new[]
                 {
                     $"/api/v1/e/{token}/polls/import",
                     $"/api/v1/e/{token}/import",
                     $"/api/v1/e/{token}/polls",
                 })
        {
            var response = await client.PostAsync(path, content);

            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEqual(HttpStatusCode.Created, response.StatusCode);
        }
    }
}
