using System.Net;
using System.Net.Http.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 009 FR-021, FR-028a, FR-029 to FR-031 and SC-004: what an Ersteller link does not grant.
/// </summary>
/// <remarks>
/// <b>Direct requests, skipping the interface entirely.</b> A control that is merely absent from
/// the creator surface is not refused; it is hidden, and hiding is not a security boundary. Every
/// call here is the one somebody would make with a terminal and the link they were sent.
/// </remarks>
public sealed class CreatorSurfaceRefusalTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private ApiFactory NewFactory() => new(storage.DataDirectory, creatorWritesPerHour: 1000);

    /// <summary>Everything that configures or maintains the installation (FR-029, FR-030).</summary>
    public static TheoryData<string, string> InstallationWideRoutes() => new()
    {
        { "GET", "/api/v1/admin/maintenance" },
        { "PUT", "/api/v1/admin/maintenance" },
        { "GET", "/api/v1/admin/backup" },
        { "POST", "/api/v1/admin/restore/preview" },
        { "POST", "/api/v1/admin/restore" },
        { "GET", "/api/v1/admin/dashboard" },
        { "GET", "/api/v1/admin/polls" },
        { "GET", "/api/v1/admin/wish-lists" },
        { "POST", "/api/v1/admin/polls/import" },
        // FR-021: the management of Ersteller links, including their own.
        { "GET", "/api/v1/admin/creators" },
        { "POST", "/api/v1/admin/creators" },
    };

    [Theory]
    [MemberData(nameof(InstallationWideRoutes))]
    public async Task A_creator_token_reaches_no_installation_wide_control(string method, string path)
    {
        using var factory = NewFactory();
        var (_, token) = await CreatorTestData.NewCreatorAsync(factory, $"Refused {Guid.NewGuid():n}");

        var client = factory.CreateClient();

        // Every way somebody might try to present the token to an admin route: in a header, as a
        // query parameter, and as a bearer credential. None of them is a session.
        client.DefaultRequestHeaders.Add("X-Creator-Token", token);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var request = new HttpRequestMessage(new HttpMethod(method), $"{path}?creatorToken={token}");
        if (method is "POST" or "PUT" or "PATCH")
        {
            request.Content = JsonContent.Create(new { enabled = true });
        }

        var response = await client.SendAsync(request);

        // 401: no operator session. Never 200, and never a payload that reveals what is there.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Holding_a_creator_token_establishes_no_operator_session()
    {
        // FR-031. The token is a capability for exactly one Ersteller's content; it is not a
        // credential that any other part of the system understands.
        using var factory = NewFactory();
        var (_, token) = await CreatorTestData.NewCreatorAsync(factory, $"Session {Guid.NewGuid():n}");

        var client = factory.CreateClient();

        // Use the link first, so any cookie the surface might set would be in hand by now.
        (await client.GetAsync($"/api/v1/e/{token}")).EnsureSuccessStatusCode();

        Assert.DoesNotContain(client.DefaultRequestHeaders, h => h.Key == "Cookie");

        var admin = await client.GetAsync("/api/v1/admin/polls");
        Assert.Equal(HttpStatusCode.Unauthorized, admin.StatusCode);
    }

    [Fact]
    public async Task No_route_beneath_a_creator_token_accepts_an_uploaded_file()
    {
        // FR-028a and SC-004. Import would put feature 005's upload-and-parse path behind the
        // weakest credential in the system. The assertion is about the *absence of a route*, not
        // about a handler refusing: there is nothing there to refuse with.
        using var factory = NewFactory();
        var (_, token) = await CreatorTestData.NewCreatorAsync(factory, $"Upload {Guid.NewGuid():n}");

        var client = factory.CreateClient();

        foreach (var path in new[]
                 {
                     $"/api/v1/e/{token}/import",
                     $"/api/v1/e/{token}/polls/import",
                     $"/api/v1/e/{token}/wish-lists/import",
                     $"/api/v1/e/{token}/restore",
                     $"/api/v1/e/{token}/backup",
                 })
        {
            using var content = new MultipartFormDataContent
            {
                { new ByteArrayContent("{}"u8.ToArray()), "file", "anything.json" },
            };

            var response = await client.PostAsync(path, content);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task A_creator_reaches_no_dashboard_and_no_aggregate_across_owners()
    {
        // FR-030. The dashboard answers "what is in here" for the installation; a figure that
        // counted across owners would tell an Ersteller how much everybody else had made.
        using var factory = NewFactory();
        var (_, token) = await CreatorTestData.NewCreatorAsync(factory, $"Dash {Guid.NewGuid():n}");

        var client = factory.CreateClient();

        foreach (var path in new[]
                 {
                     $"/api/v1/e/{token}/dashboard",
                     $"/api/v1/e/{token}/statistics",
                     $"/api/v1/e/{token}/creators",
                 })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        }
    }

    [Fact]
    public async Task An_Ersteller_cannot_manage_any_Ersteller_including_their_own()
    {
        // FR-021, at the routes rather than in the abstract. Revoking or deleting one's own
        // Ersteller is as forbidden as touching somebody else's - there is no self-service here,
        // by design (Out of Scope).
        using var factory = NewFactory();
        var (id, token) = await CreatorTestData.NewCreatorAsync(factory, $"Self {Guid.NewGuid():n}");

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Creator-Token", token);

        foreach (var path in new[]
                 {
                     $"/api/v1/admin/creators/{id}",
                     $"/api/v1/admin/creators/{id}/link",
                 })
        {
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                (await client.DeleteAsync(path)).StatusCode);
        }

        // And the Ersteller is still there, with a working link.
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync($"/api/v1/e/{token}")).StatusCode);
    }
}
