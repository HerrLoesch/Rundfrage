using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 009 US4: revoke, reissue, rename and delete (FR-016 to FR-020c).
/// </summary>
/// <remarks>
/// <b>The suite that carries the spec's most consequential clarification.</b> Revoking takes the
/// link away and destroys nothing; deleting destroys the Ersteller together with everything it
/// owns. Those are different actions with different consequences, and the difference is the whole
/// of the spec's first question.
/// <para>
/// The delete case is also the only thing that catches EF Core's optional-relationship default. At
/// <c>ClientSetNull</c>, deleting an Ersteller would set <c>CreatorId</c> to null on its content -
/// handing it to the operator instead of destroying it - and nothing would report an error
/// (research R-4).
/// </para>
/// </remarks>
public sealed class CreatorRevocationTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private ApiFactory NewFactory() => new(storage.DataDirectory, creatorWritesPerHour: 1000);

    private sealed record Stage(
        HttpClient Admin, Guid Id, string Token, string Name,
        Guid PollId, string ParticipantToken, Guid ListId, string ListToken);

    private static async Task<Stage> StageAsync(ApiFactory factory)
    {
        var name = $"Anna {Guid.NewGuid():n}";
        var (id, token) = await CreatorTestData.NewCreatorAsync(factory, name);

        var anna = factory.CreateClient();
        var pollId = await CreatorTestData.NewPollAsync(anna, token, $"Termin {Guid.NewGuid():n}");
        var listId = await CreatorTestData.NewWishListAsync(anna, token, $"Liste {Guid.NewGuid():n}");

        var surface = await anna.GetFromJsonAsync<JsonElement>($"/api/v1/e/{token}");
        var participantToken = surface.GetProperty("polls").EnumerateArray()
            .Single().GetProperty("participantToken").GetString()!;
        var listToken = surface.GetProperty("wishLists").EnumerateArray()
            .Single().GetProperty("listToken").GetString()!;

        return new Stage(
            await factory.CreateSignedInClientAsync(),
            id, token, name, pollId, participantToken, listId, listToken);
    }

    private async Task<int[]> CountsAsync()
    {
        await using var db = new RundfrageDbContext(
            new DbContextOptionsBuilder<RundfrageDbContext>()
                .UseSqlite(StorageLocation.ConnectionStringFor(storage.DataDirectory)).Options);

        return
        [
            await db.Polls.CountAsync(),
            await db.WishLists.CountAsync(),
            await db.Responses.CountAsync(),
            await db.WishClaims.CountAsync(),
        ];
    }

    [Fact]
    public async Task Revoking_takes_the_link_away_and_destroys_nothing()
    {
        // FR-018, FR-020 and SC-006a.
        using var factory = NewFactory();
        var stage = await StageAsync(factory);

        var before = await CountsAsync();

        var revoked = await stage.Admin.DeleteAsync($"/api/v1/admin/creators/{stage.Id}/link");
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);

        // The link stops working immediately.
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await factory.CreateClient().GetAsync($"/api/v1/e/{stage.Token}")).StatusCode);

        // And nothing at all was removed.
        Assert.Equal(before, await CountsAsync());

        // Including the participant links, which keep answering - the people Anna sent them to
        // have no idea any of this happened, and should not.
        Assert.Equal(
            HttpStatusCode.OK,
            (await factory.CreateClient().GetAsync($"/api/v1/polls/{stage.ParticipantToken}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await factory.CreateClient().GetAsync($"/api/v1/wish-lists/{stage.ListToken}")).StatusCode);
    }

    [Fact]
    public async Task A_revoked_Ersteller_stays_listed_with_its_counts_and_no_link()
    {
        // FR-044a. Not hidden and not sorted away: the operator's next decision - new link, or
        // delete - needs exactly these figures in front of them.
        using var factory = NewFactory();
        var stage = await StageAsync(factory);

        await stage.Admin.DeleteAsync($"/api/v1/admin/creators/{stage.Id}/link");

        var creators = await stage.Admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/creators");
        var row = creators.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == stage.Id);

        Assert.False(row.GetProperty("hasLink").GetBoolean());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("linkToken").ValueKind);
        Assert.Equal(stage.Name, row.GetProperty("name").GetString());
        Assert.Equal(1, row.GetProperty("pollCount").GetInt32());
        Assert.Equal(1, row.GetProperty("wishListCount").GetInt32());
    }

    [Fact]
    public async Task A_new_link_reaches_exactly_what_the_Ersteller_owned_before()
    {
        // FR-016 and SC-007, including the revoked case: issuing a link is how a revoked Ersteller
        // is given access again, because there is no separate "unrevoke".
        using var factory = NewFactory();
        var stage = await StageAsync(factory);

        await stage.Admin.DeleteAsync($"/api/v1/admin/creators/{stage.Id}/link");

        var reissued = await stage.Admin.PostAsync($"/api/v1/admin/creators/{stage.Id}/link", null);
        reissued.EnsureSuccessStatusCode();

        var newToken = (await reissued.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("linkToken").GetString()!;

        Assert.NotEqual(stage.Token, newToken);

        // The previous URL is indistinguishable from one that never existed, within one request.
        var old = await factory.CreateClient().GetAsync($"/api/v1/e/{stage.Token}");
        var invented = await factory.CreateClient().GetAsync("/api/v1/e/abcdefghijklmnopqrstuv");
        Assert.Equal(HttpStatusCode.NotFound, old.StatusCode);
        Assert.Equal(
            await invented.Content.ReadAsStringAsync(),
            await old.Content.ReadAsStringAsync());

        // Zero items lost and zero gained.
        var surface = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/e/{newToken}");
        Assert.Equal(stage.PollId, surface.GetProperty("polls").EnumerateArray().Single()
            .GetProperty("id").GetGuid());
        Assert.Equal(stage.ListId, surface.GetProperty("wishLists").EnumerateArray().Single()
            .GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Renaming_changes_the_name_and_nothing_else()
    {
        // FR-017. The link keeps working and the content is untouched - which is the whole
        // difference between renaming and reissuing.
        using var factory = NewFactory();
        var stage = await StageAsync(factory);

        var newName = $"Anna neu {Guid.NewGuid():n}";
        var renamed = await stage.Admin.PatchAsJsonAsync(
            $"/api/v1/admin/creators/{stage.Id}", new { name = newName });
        renamed.EnsureSuccessStatusCode();

        var body = await renamed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(newName, body.GetProperty("name").GetString());
        Assert.Equal(stage.Token, body.GetProperty("linkToken").GetString());

        // The link still works, and reaches the same content.
        var surface = await factory.CreateClient()
            .GetFromJsonAsync<JsonElement>($"/api/v1/e/{stage.Token}");
        Assert.Equal(newName, surface.GetProperty("name").GetString());
        Assert.Single(surface.GetProperty("polls").EnumerateArray());
        Assert.Single(surface.GetProperty("wishLists").EnumerateArray());

        // And the new name is what the owner column says from now on (FR-039).
        var polls = await stage.Admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/polls");
        Assert.Equal(
            newName,
            polls.EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == stage.PollId)
                .GetProperty("creatorName").GetString());
    }

    [Fact]
    public async Task Deleting_destroys_the_Ersteller_and_everything_it_owns()
    {
        // FR-020a. The other half of the spec's first clarification, and the test that catches
        // EF Core's ClientSetNull default (research R-4).
        using var factory = NewFactory();
        var stage = await StageAsync(factory);

        var deleted = await stage.Admin.DeleteAsync($"/api/v1/admin/creators/{stage.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        await using var db = new RundfrageDbContext(
            new DbContextOptionsBuilder<RundfrageDbContext>()
                .UseSqlite(StorageLocation.ConnectionStringFor(storage.DataDirectory)).Options);

        // Destroyed, not reassigned. The distinction is the point: a poll with CreatorId = null
        // would still be in the table, would appear in the admin list as the operator's own, and
        // every count in this suite except this one would look correct (FR-020c).
        Assert.False(await db.Polls.AnyAsync(p => p.Id == stage.PollId));
        Assert.False(await db.WishLists.AnyAsync(l => l.Id == stage.ListId));
        Assert.False(await db.Creators.AnyAsync(c => c.Id == stage.Id));

        // And the links those produced behave as unknown links.
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await factory.CreateClient().GetAsync($"/api/v1/polls/{stage.ParticipantToken}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await factory.CreateClient().GetAsync($"/api/v1/wish-lists/{stage.ListToken}")).StatusCode);
    }

    [Fact]
    public async Task Deleting_one_Ersteller_touches_nothing_belonging_to_another_owner()
    {
        using var factory = NewFactory();
        var doomed = await StageAsync(factory);
        var spared = await StageAsync(factory);

        // Something of the operator's own, too - the owner whose rows carry a null CreatorId and
        // would be the ones a careless cascade swept up.
        var operatorsPoll = (await (await doomed.Admin.PostAsJsonAsync("/api/v1/admin/polls", new
        {
            title = $"Betreiber {Guid.NewGuid():n}",
            days = new[] { "2026-12-05" },
        })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await doomed.Admin.DeleteAsync($"/api/v1/admin/creators/{doomed.Id}");

        await using var db = new RundfrageDbContext(
            new DbContextOptionsBuilder<RundfrageDbContext>()
                .UseSqlite(StorageLocation.ConnectionStringFor(storage.DataDirectory)).Options);

        Assert.True(await db.Polls.AnyAsync(p => p.Id == spared.PollId));
        Assert.True(await db.WishLists.AnyAsync(l => l.Id == spared.ListId));
        Assert.True(await db.Polls.AnyAsync(p => p.Id == operatorsPoll));
        Assert.True(await db.Creators.AnyAsync(c => c.Id == spared.Id));
    }

    [Fact]
    public async Task Revoking_and_deleting_are_separate_operations_on_separate_addresses()
    {
        // FR-020b, at the level the API can express it: the link is a sub-resource, so DELETE on
        // it plainly removes a link and DELETE on the Ersteller plainly removes an Ersteller.
        // Neither is a variant of the other, and neither takes a flag that turns it into the other.
        using var factory = NewFactory();
        var stage = await StageAsync(factory);

        var before = await CountsAsync();

        (await stage.Admin.DeleteAsync($"/api/v1/admin/creators/{stage.Id}/link")).EnsureSuccessStatusCode();
        Assert.Equal(before, await CountsAsync());

        (await stage.Admin.DeleteAsync($"/api/v1/admin/creators/{stage.Id}")).EnsureSuccessStatusCode();
        Assert.NotEqual(before, await CountsAsync());
    }

    [Fact]
    public async Task Managing_an_Ersteller_that_does_not_exist_answers_neutrally()
    {
        // FR-010: refused without revealing whether the named Ersteller exists.
        using var factory = NewFactory();
        var stage = await StageAsync(factory);

        var invented = Guid.CreateVersion7();

        foreach (var response in new[]
                 {
                     await stage.Admin.DeleteAsync($"/api/v1/admin/creators/{invented}"),
                     await stage.Admin.DeleteAsync($"/api/v1/admin/creators/{invented}/link"),
                     await stage.Admin.PostAsync($"/api/v1/admin/creators/{invented}/link", null),
                 })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("{\"code\":\"not_found\"}", await response.Content.ReadAsStringAsync());
        }
    }
}
