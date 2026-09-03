using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// What a reverse proxy changes about the two things the application reads off the connection:
/// the request source FR-027a partitions by, and the scheme that decides whether the session
/// cookie carries <c>Secure</c>.
/// </summary>
/// <remarks>
/// Both are read from the connection, and behind a proxy the connection is the proxy's. The
/// failures are silent - a poll that refuses its eleventh participant, a cookie without a flag -
/// which is why they are asserted here rather than left to be discovered in a deployment.
/// </remarks>
public class ReverseProxyTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private const string ProxiedScheme = "X-Forwarded-Proto";
    private const string ProxiedSource = "X-Forwarded-For";

    private static HttpClient ClientBehindProxy(
        ApiFactory factory, string? forwardedFor = null, string? forwardedProto = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        if (forwardedFor is not null)
        {
            client.DefaultRequestHeaders.Add(ProxiedSource, forwardedFor);
        }

        if (forwardedProto is not null)
        {
            client.DefaultRequestHeaders.Add(ProxiedScheme, forwardedProto);
        }

        return client;
    }

    private static async Task<(string Token, Guid DayId)> CreatePollAsync(ApiFactory factory)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var created = await admin.PostAsJsonAsync(
            "/api/v1/admin/polls", new { title = "Hinter dem Proxy", days = new[] { "2026-11-20" } });

        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        var token = summary.GetProperty("participantToken").GetString()!;

        var view = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{token}");
        var dayId = view.GetProperty("days")[0].GetProperty("id").GetGuid();

        return (token, dayId);
    }

    private static Task<HttpResponseMessage> SubmitAsync(
        HttpClient client, string token, Guid dayId, string name) =>
        client.PostAsJsonAsync($"/api/v1/polls/{token}/responses", new
        {
            displayName = name,
            answers = new[] { new { dayId, availability = "yes" } },
        });

    [Fact]
    public async Task Each_participant_behind_a_trusted_proxy_gets_their_own_budget()
    {
        // FR-027a says ten per hour per *source*. Without this the source is the proxy, one poll
        // shares a single budget, and the eleventh person to answer is refused for no reason.
        using var factory = new ApiFactory(storage.DataDirectory, submissionsPerHour: 2, trustedProxies: 1);
        var (token, dayId) = await CreatePollAsync(factory);

        var anna = ClientBehindProxy(factory, forwardedFor: "203.0.113.7");
        var bert = ClientBehindProxy(factory, forwardedFor: "203.0.113.8");

        Assert.Equal(HttpStatusCode.Created, (await SubmitAsync(anna, token, dayId, "Anna 1")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SubmitAsync(anna, token, dayId, "Anna 2")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await SubmitAsync(anna, token, dayId, "Anna 3")).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await SubmitAsync(bert, token, dayId, "Bert")).StatusCode);
    }

    [Fact]
    public async Task A_forwarded_source_is_ignored_when_no_proxy_is_trusted()
    {
        // The direct deployment. Here the header is the caller's own claim about themselves, and
        // honouring it would let one machine exhaust nothing but its own budget by renaming
        // itself between requests - which is to say, no budget at all.
        using var factory = new ApiFactory(storage.DataDirectory, submissionsPerHour: 2);
        var (token, dayId) = await CreatePollAsync(factory);

        var claimed = ClientBehindProxy(factory, forwardedFor: "203.0.113.7");
        var alsoClaimed = ClientBehindProxy(factory, forwardedFor: "203.0.113.8");

        await SubmitAsync(claimed, token, dayId, "Erste");
        await SubmitAsync(claimed, token, dayId, "Zweite");

        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await SubmitAsync(alsoClaimed, token, dayId, "Dritte")).StatusCode);
    }

    [Fact]
    public async Task The_session_cookie_is_secure_when_the_trusted_proxy_terminated_TLS()
    {
        // The proxy speaks HTTPS to the browser and plain HTTP to us. Without the forwarded
        // scheme the cookie policy sees that plain request and issues the session without the
        // flag, on a connection the browser considers secure.
        using var factory = new ApiFactory(storage.DataDirectory, trustedProxies: 1);
        var client = ClientBehindProxy(factory, forwardedProto: "https");

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/session",
            new { user = ApiFactory.TestUser, password = ApiFactory.TestPassword });

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_session_cookie_follows_a_plain_request_when_no_proxy_is_trusted()
    {
        // `docker compose up` on http://localhost is a supported way to run this. A cookie
        // marked Secure there is one the browser may decline to send back, so the flag follows
        // the real scheme rather than being asserted unconditionally.
        using var factory = new ApiFactory(storage.DataDirectory);
        var client = ClientBehindProxy(factory, forwardedProto: "https");

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/session",
            new { user = ApiFactory.TestUser, password = ApiFactory.TestPassword });

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }
}
