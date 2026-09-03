using System.Net;
using System.Net.Http.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>FR-001, FR-004 to FR-007 and research.md R-1.</summary>
public class SignInEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new(ApiFactory.UnreachableDirectory);

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private HttpClient NonRedirectingClient() =>
        _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

    [Fact]
    public async Task Correct_credentials_return_204_and_set_the_session_cookie()
    {
        var client = NonRedirectingClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/session",
            new { user = ApiFactory.TestUser, password = ApiFactory.TestPassword });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("rundfrage.session", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        // SameSite=Strict is what removes forged-form CSRF without a token mechanism (R-10).
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_wrong_user_and_a_wrong_password_are_indistinguishable()
    {
        // FR-004: a failure must not say which half was wrong.
        var client = NonRedirectingClient();

        var wrongUser = await client.PostAsJsonAsync(
            "/api/v1/admin/session", new { user = "someone-else", password = ApiFactory.TestPassword });
        var wrongPassword = await client.PostAsJsonAsync(
            "/api/v1/admin/session", new { user = ApiFactory.TestUser, password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongUser.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(await wrongUser.Content.ReadAsStringAsync(), await wrongPassword.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_failure_discloses_nothing_about_the_account()
    {
        var client = NonRedirectingClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/session", new { user = "nobody", password = "nothing" });

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(ApiFactory.TestUser, body);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("user", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Five_failures_lock_the_account_and_a_correct_password_is_refused_too()
    {
        // FR-005, FR-005a, SC-019. The lockout must not become an oracle: if a correct password
        // succeeded during it, the lockout would answer "was that the right password?".
        var client = NonRedirectingClient();

        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync(
                "/api/v1/admin/session", new { user = ApiFactory.TestUser, password = "wrong" });
        }

        var withCorrectPassword = await client.PostAsJsonAsync(
            "/api/v1/admin/session",
            new { user = ApiFactory.TestUser, password = ApiFactory.TestPassword });

        Assert.Equal(HttpStatusCode.TooManyRequests, withCorrectPassword.StatusCode);

        var payload = await withCorrectPassword.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("account_locked", payload.GetProperty("code").GetString());
        Assert.True(payload.GetProperty("retryAfterSeconds").GetInt32() > 0);
    }

    [Fact]
    public async Task Signing_out_clears_the_session()
    {
        // FR-007
        var client = await _factory.CreateSignedInClientAsync();

        var beforeSignOut = await client.GetAsync("/api/v1/admin/polls");
        var signOut = await client.DeleteAsync("/api/v1/admin/session");
        var afterSignOut = await client.GetAsync("/api/v1/admin/polls");

        Assert.NotEqual(HttpStatusCode.Unauthorized, beforeSignOut.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterSignOut.StatusCode);
    }
}
