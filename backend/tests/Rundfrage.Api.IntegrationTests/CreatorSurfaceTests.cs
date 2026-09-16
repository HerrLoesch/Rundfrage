using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 009 US1: what a holder reaches through their link, and what an unusable link answers.
/// </summary>
public sealed class CreatorSurfaceTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private ApiFactory NewFactory() => new(storage.DataDirectory);

    [Fact]
    public async Task A_link_opens_onto_the_holders_own_two_lists_with_no_session()
    {
        // FR-006: no account, no sign-in, no password, no confirmation, and no step of any kind
        // between the link and the work. The client below has never signed in and carries no
        // cookie - which is the whole assertion.
        using var factory = NewFactory();
        var (_, token) = await CreatorTestData.NewCreatorAsync(factory, "Anna eins");

        var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/e/{token}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // FR-007: the name, so the holder can tell which link they are using.
        Assert.Equal("Anna eins", body.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Array, body.GetProperty("polls").ValueKind);
        Assert.Equal(JsonValueKind.Array, body.GetProperty("wishLists").ValueKind);
    }

    [Fact]
    public async Task The_surface_carries_no_dashboard_and_no_figure_across_the_two_lists()
    {
        // FR-028b. Two lists are the whole overview: a person with two short lists in front of
        // them does not need a third page telling them how many are on each.
        using var factory = NewFactory();
        var (_, token) = await CreatorTestData.NewCreatorAsync(factory, "Anna zwei");

        var body = await factory.CreateClient()
            .GetFromJsonAsync<JsonElement>($"/api/v1/e/{token}");

        var properties = body.EnumerateObject().Select(p => p.Name).ToArray();

        Assert.Equal(["name", "polls", "wishLists"], properties.Order());
    }

    [Theory]
    [InlineData("abcdefghijklmnopqrstuv")] // well-formed, never existed
    [InlineData("too-short")]              // malformed
    [InlineData("!!!!!!!!!!!!!!!!!!!!!!")] // wrong alphabet
    public async Task An_unusable_link_answers_the_one_neutral_payload(string token)
    {
        using var factory = NewFactory();

        var response = await factory.CreateClient().GetAsync($"/api/v1/e/{token}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("{\"code\":\"not_found\"}", body);
    }

    [Fact]
    public async Task A_revoked_link_is_indistinguishable_from_one_that_never_existed()
    {
        // FR-008 and FR-020. This is the single worst bug this feature could have: a revoked link
        // that still resolves. It cannot happen by a filter being forgotten, because there is no
        // filter - the token is simply no longer in the table (research R-3) - but the neutral
        // payload matters too, or an observer could tell "was revoked" from "never existed".
        using var factory = NewFactory();
        var (id, token) = await CreatorTestData.NewCreatorAsync(factory, "Anna drei");

        var admin = await factory.CreateSignedInClientAsync();
        (await admin.DeleteAsync($"/api/v1/admin/creators/{id}/link")).EnsureSuccessStatusCode();

        var revoked = await factory.CreateClient().GetAsync($"/api/v1/e/{token}");
        var invented = await factory.CreateClient().GetAsync("/api/v1/e/abcdefghijklmnopqrstuv");

        Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
        Assert.Equal(
            await invented.Content.ReadAsStringAsync(),
            await revoked.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_empty_token_does_not_resolve_to_a_revoked_Ersteller()
    {
        // A revoked Ersteller's LinkToken is NULL. A lookup that compared a null or empty token
        // against it would hand a revoked link's holder their access straight back - and, worse,
        // would hand it to anybody who guessed the empty string.
        using var factory = NewFactory();
        var (id, _) = await CreatorTestData.NewCreatorAsync(factory, "Anna vier");

        var admin = await factory.CreateSignedInClientAsync();
        (await admin.DeleteAsync($"/api/v1/admin/creators/{id}/link")).EnsureSuccessStatusCode();

        // "/api/v1/e/" with nothing after it does not match the route at all and lands on the
        // catch-all, which answers the same neutral payload (Program.cs).
        var empty = await factory.CreateClient().GetAsync("/api/v1/e/");

        Assert.Equal(HttpStatusCode.NotFound, empty.StatusCode);
        Assert.Equal("{\"code\":\"not_found\"}", await empty.Content.ReadAsStringAsync());
    }
}
