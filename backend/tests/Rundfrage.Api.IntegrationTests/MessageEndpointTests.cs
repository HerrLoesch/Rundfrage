using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// Contract test for GET /api/v1/message (FR-006, contracts/openapi.yaml).
/// Needs no database: the endpoint must answer regardless of database state.
/// </summary>
public class MessageEndpointTests : IDisposable
{
    // Constructed here rather than as a class fixture: xUnit's fixture activator needs a
    // parameterless constructor, and ApiFactory deliberately requires a connection string so no
    // test can accidentally run against the wrong database. This endpoint needs none.
    private readonly ApiFactory _factory = new(ApiFactory.UnreachableConnection);

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Returns_200_with_a_non_empty_message()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/message");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var message = payload.GetProperty("message").GetString();
        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    [Fact]
    public async Task Response_carries_only_the_message_property()
    {
        // contracts/openapi.yaml declares additionalProperties: false.
        var client = _factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>("/api/v1/message");

        var names = payload.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(["message"], names);
    }

    [Fact]
    public async Task Unknown_api_paths_return_404_rather_than_the_SPA_shell()
    {
        // FR-006a: /api/v1 belongs to the API; a miss there must not fall through to index.html.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
