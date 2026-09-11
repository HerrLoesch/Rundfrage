using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>T041: the contract for reading and setting maintenance state.</summary>
public class MaintenanceEndpointTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    [Fact]
    public async Task Reading_the_state_requires_an_operator_session()
    {
        using var factory = new ApiFactory(storage.DataDirectory);

        var response = await factory.CreateClient().GetAsync("/api/v1/admin/maintenance");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Setting_the_state_requires_an_operator_session()
    {
        using var factory = new ApiFactory(storage.DataDirectory);

        var response = await factory.CreateClient().PutAsJsonAsync(
            "/api/v1/admin/maintenance", new { enabled = true });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reports_off_by_default_and_carries_no_moment()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var state = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/maintenance");

        Assert.False(state.GetProperty("enabled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, state.GetProperty("since").ValueKind);
    }

    [Fact]
    public async Task Reports_on_with_the_moment_it_was_switched_on()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        try
        {
            var set = await admin.PutAsJsonAsync("/api/v1/admin/maintenance", new { enabled = true });
            var body = await set.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.OK, set.StatusCode);
            Assert.True(body.GetProperty("enabled").GetBoolean());
            Assert.Equal(JsonValueKind.String, body.GetProperty("since").ValueKind);
        }
        finally
        {
            await admin.PutAsJsonAsync("/api/v1/admin/maintenance", new { enabled = false });
        }
    }

    [Fact]
    public async Task Switching_to_the_state_it_is_already_in_succeeds()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var first = await admin.PutAsJsonAsync("/api/v1/admin/maintenance", new { enabled = false });
        var second = await admin.PutAsJsonAsync("/api/v1/admin/maintenance", new { enabled = false });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }
}
