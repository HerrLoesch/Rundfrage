using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 009 US3: the operator sees everything, sees whose it is, and may do anything to any of it
/// (FR-038 to FR-042, FR-053, SC-005, SC-005a, SC-005b).
/// </summary>
public sealed class CreatorOwnershipTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private ApiFactory NewFactory() => new(storage.DataDirectory, creatorWritesPerHour: 1000);

    private sealed record Stage(
        HttpClient Admin, string AnnaName, string PollTitle, Guid AnnasPoll, Guid AnnasList,
        Guid OperatorsPoll, Guid OperatorsList);

    private static async Task<Stage> StageAsync(ApiFactory factory)
    {
        var annaName = $"Anna {Guid.NewGuid():n}";
        var (_, annaToken) = await CreatorTestData.NewCreatorAsync(factory, annaName);

        var anna = factory.CreateClient();

        // Unique per test. SqliteFixture is per class, so every test in this class shares one
        // storage file and a fixed title would be ambiguous by the second test - which is how the
        // import assertion below first failed, counting rows an earlier test had left behind.
        var pollTitle = $"Annas Termin {Guid.NewGuid():n}";
        var annasPoll = await CreatorTestData.NewPollAsync(anna, annaToken, pollTitle);
        var annasList = await CreatorTestData.NewWishListAsync(anna, annaToken, "Annas Liste");

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

        return new Stage(
            admin, annaName, pollTitle, annasPoll, annasList, operatorsPoll, operatorsList);
    }

    [Fact]
    public async Task Every_poll_is_listed_whoever_owns_it_and_each_row_names_its_owner()
    {
        using var factory = NewFactory();
        var stage = await StageAsync(factory);

        var polls = await stage.Admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/polls");
        var rows = polls.EnumerateArray().ToDictionary(p => p.GetProperty("id").GetGuid());

        Assert.Contains(stage.AnnasPoll, rows.Keys);
        Assert.Contains(stage.OperatorsPoll, rows.Keys);

        // FR-039: the Ersteller's name, or that it is the operator's own.
        Assert.Equal(stage.AnnaName, rows[stage.AnnasPoll].GetProperty("creatorName").GetString());

        // The operator's own carries null rather than an empty string - an empty cell reads as
        // missing data, and the interface renders the null as "Eigene" (ui-contract section 5).
        Assert.Equal(JsonValueKind.Null, rows[stage.OperatorsPoll].GetProperty("creatorName").ValueKind);
        Assert.Equal(JsonValueKind.Null, rows[stage.OperatorsPoll].GetProperty("creatorId").ValueKind);
    }

    [Fact]
    public async Task Every_wish_list_is_listed_whoever_owns_it_and_each_row_names_its_owner()
    {
        using var factory = NewFactory();
        var stage = await StageAsync(factory);

        var lists = await stage.Admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/wish-lists");
        var rows = lists.EnumerateArray().ToDictionary(l => l.GetProperty("id").GetGuid());

        Assert.Contains(stage.AnnasList, rows.Keys);
        Assert.Contains(stage.OperatorsList, rows.Keys);
        Assert.Equal(stage.AnnaName, rows[stage.AnnasList].GetProperty("creatorName").GetString());
        Assert.Equal(JsonValueKind.Null, rows[stage.OperatorsList].GetProperty("creatorName").ValueKind);
    }

    [Fact]
    public async Task Every_admin_action_behaves_identically_whoever_owns_the_thing()
    {
        // FR-040 and SC-005a: zero refusals attributable to ownership. The pairs below are the
        // same call against the operator's own content and against an Ersteller's.
        using var factory = NewFactory();
        var stage = await StageAsync(factory);

        async Task BothAsync(string description, Func<Guid, Task<HttpResponseMessage>> call,
            Guid mine, Guid theirs)
        {
            var onMine = await call(mine);
            var onTheirs = await call(theirs);

            Assert.True(
                onMine.StatusCode == onTheirs.StatusCode,
                $"{description}: {onMine.StatusCode} on the operator's own, "
                + $"{onTheirs.StatusCode} on an Ersteller's");
        }

        await BothAsync("read results",
            id => stage.Admin.GetAsync($"/api/v1/admin/polls/{id}"),
            stage.OperatorsPoll, stage.AnnasPoll);

        await BothAsync("export",
            id => stage.Admin.GetAsync($"/api/v1/admin/polls/{id}/export"),
            stage.OperatorsPoll, stage.AnnasPoll);

        await BothAsync("read a wish list",
            id => stage.Admin.GetAsync($"/api/v1/admin/wish-lists/{id}"),
            stage.OperatorsList, stage.AnnasList);

        await BothAsync("edit a wish list",
            id => stage.Admin.PatchAsJsonAsync(
                $"/api/v1/admin/wish-lists/{id}", new { title = "Vom Betreiber geändert" }),
            stage.OperatorsList, stage.AnnasList);

        await BothAsync("add an item",
            id => stage.Admin.PostAsJsonAsync(
                $"/api/v1/admin/wish-lists/{id}/items", new { name = "Vom Betreiber", wantedCount = 1 }),
            stage.OperatorsList, stage.AnnasList);

        await BothAsync("delete a wish list",
            id => stage.Admin.DeleteAsync($"/api/v1/admin/wish-lists/{id}"),
            stage.OperatorsList, stage.AnnasList);

        await BothAsync("delete a poll",
            id => stage.Admin.DeleteAsync($"/api/v1/admin/polls/{id}"),
            stage.OperatorsPoll, stage.AnnasPoll);
    }

    [Fact]
    public async Task An_edit_by_the_operator_does_not_change_who_owns_the_thing()
    {
        // FR-040a and SC-005b. The operator may rewrite an Ersteller's list; doing so must not
        // quietly make it theirs, or the owner column would start telling a different story than
        // the one FR-039 promises.
        using var factory = NewFactory();
        var stage = await StageAsync(factory);

        (await stage.Admin.PatchAsJsonAsync(
            $"/api/v1/admin/wish-lists/{stage.AnnasList}",
            new { title = "Vom Betreiber umbenannt" })).EnsureSuccessStatusCode();

        var lists = await stage.Admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/wish-lists");
        var row = lists.EnumerateArray().Single(l => l.GetProperty("id").GetGuid() == stage.AnnasList);

        Assert.Equal("Vom Betreiber umbenannt", row.GetProperty("title").GetString());
        Assert.Equal(stage.AnnaName, row.GetProperty("creatorName").GetString());
    }

    [Fact]
    public async Task The_dashboard_counts_every_owners_content_and_names_wish_list_owners()
    {
        // FR-041 and FR-042. Installation-wide, never split per Ersteller: the dashboard answers
        // "what is in here", and a per-owner split would answer a question nobody asked.
        using var factory = NewFactory();
        var stage = await StageAsync(factory);

        var dashboard = await stage.Admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/dashboard");
        var lists = await stage.Admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/wish-lists");

        await using var db = new RundfrageDbContext(
            new DbContextOptionsBuilder<RundfrageDbContext>()
                .UseSqlite(StorageLocation.ConnectionStringFor(storage.DataDirectory)).Options);

        Assert.Equal(await db.Polls.CountAsync(), dashboard.GetProperty("pollCount").GetInt32());

        // FR-042: an Ersteller's name is operator-written text, permitted here on the same grounds
        // 008 FR-048b permitted wish-list titles.
        Assert.Contains(
            lists.EnumerateArray(),
            l => l.GetProperty("creatorName").GetString() == stage.AnnaName);
    }

    [Fact]
    public async Task No_participant_display_name_appears_on_the_dashboard()
    {
        // 008 FR-050, restated because this feature adds a name-shaped field beside the figures
        // and the two must not be confused. An Ersteller's name is the operator's own label; a
        // participant's is not.
        using var factory = NewFactory();
        var stage = await StageAsync(factory);

        var raw = await stage.Admin.GetStringAsync("/api/v1/admin/dashboard");

        foreach (var forbidden in new[] { "displayName", "names", "participant" })
        {
            Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task A_poll_the_operator_imports_belongs_to_the_operator()
    {
        // FR-053. Import is an operator capability that no Ersteller can reach (FR-028a), so there
        // is no owner to pass and no rule to guess at - but the column still has to say so.
        using var factory = NewFactory();
        var stage = await StageAsync(factory);

        var document = await stage.Admin.GetStringAsync(
            $"/api/v1/admin/polls/{stage.AnnasPoll}/export");

        using var upload = new MultipartFormDataContent
        {
            { new StringContent(document), "file", "poll.json" },
        };

        var imported = await stage.Admin.PostAsync("/api/v1/admin/polls/import", upload);
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);

        var polls = await stage.Admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/polls");
        var copies = polls.EnumerateArray()
            .Where(p => p.GetProperty("title").GetString() == stage.PollTitle)
            .ToArray();

        // The original is Anna's; the imported copy is the operator's. Importing does not
        // reproduce the ownership recorded in the file - there is none in it.
        Assert.Equal(2, copies.Length);
        Assert.Contains(copies, p => p.GetProperty("creatorName").GetString() == stage.AnnaName);
        Assert.Contains(copies, p => p.GetProperty("creatorName").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task Retention_is_indifferent_to_ownership()
    {
        // FR-014, as a guard. Expected to pass on its first run: RetentionService does not know
        // that owners exist and must not learn. It exists to fail if somebody ever scopes the
        // sweep, which would leave an Ersteller's expired polls on disk for ever.
        using var factory = NewFactory();
        var stage = await StageAsync(factory);

        await using var db = new RundfrageDbContext(
            new DbContextOptionsBuilder<RundfrageDbContext>()
                .UseSqlite(StorageLocation.ConnectionStringFor(storage.DataDirectory)).Options);

        // Expire Anna's poll and the operator's alike.
        await db.Polls
            .Where(p => p.Id == stage.AnnasPoll || p.Id == stage.OperatorsPoll)
            .ExecuteUpdateAsync(s => s.SetProperty(
                p => p.RetentionDeadline, DateTime.UtcNow.AddDays(-1)));

        var polls = await stage.Admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/polls");
        var ids = polls.EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToArray();

        Assert.DoesNotContain(stage.AnnasPoll, ids);
        Assert.DoesNotContain(stage.OperatorsPoll, ids);
    }
}
