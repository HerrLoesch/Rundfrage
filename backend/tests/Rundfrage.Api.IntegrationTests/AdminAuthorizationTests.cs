using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// FR-002, FR-048, SC-004: <b>every</b> admin function refuses without a session and discloses
/// nothing about what exists.
/// </summary>
/// <remarks>
/// The routes are discovered from the running application's endpoint table rather than listed
/// here. A hand-maintained list asserts only what someone remembered to add to it, and would
/// have gone stale the moment a new admin endpoint appeared - which is precisely the failure
/// FR-048 is written to prevent. This way a new admin route is covered the day it is added.
/// </remarks>
public class AdminAuthorizationTests : IDisposable
{
    private const string AdminPrefix = "/api/v1/admin";

    private readonly ApiFactory _factory = new(ApiFactory.UnreachableDirectory);

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private IReadOnlyList<RouteEndpoint> AdminEndpoints()
    {
        // Forces the host to build so the endpoint table is populated.
        _ = _factory.Services;

        return _factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith(AdminPrefix, StringComparison.Ordinal) == true)
            .ToList();
    }

    /// <summary>The only admin endpoints that may be anonymous: signing in and out themselves.</summary>
    private static bool IsSessionEndpoint(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText?.EndsWith("/session", StringComparison.Ordinal) == true;

    [Fact]
    public void There_is_something_to_test()
    {
        // Guards against the discovery silently finding nothing and the suite passing vacuously.
        Assert.NotEmpty(AdminEndpoints());
    }

    [Fact]
    public void Every_admin_endpoint_except_the_session_ones_requires_authorization()
    {
        var unprotected = AdminEndpoints()
            .Where(e => !IsSessionEndpoint(e))
            .Where(e => e.Metadata.GetMetadata<IAuthorizeData>() is null
                        || e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => e.RoutePattern.RawText)
            .ToList();

        Assert.True(
            unprotected.Count == 0,
            "These admin endpoints do not require a session (FR-048): " + string.Join(", ", unprotected));
    }

    [Fact]
    public void Only_the_session_endpoints_are_anonymous()
    {
        var anonymous = AdminEndpoints()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => e.RoutePattern.RawText!)
            .Distinct()
            .ToList();

        Assert.All(anonymous, route => Assert.EndsWith("/session", route));
    }

    [Fact]
    public async Task Every_protected_admin_route_answers_401_without_a_session()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        foreach (var endpoint in AdminEndpoints().Where(e => !IsSessionEndpoint(e)))
        {
            var method = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.First() ?? "GET";
            var path = SubstituteRouteParameters(endpoint.RoutePattern.RawText!);

            var request = new HttpRequestMessage(new HttpMethod(method), path);
            if (method is "POST" or "PUT")
            {
                request.Content = JsonContent.Create(new { title = "x", days = new[] { "2026-10-15" } });
            }

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("{\"code\":\"unauthorized\"}", await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Refusal_is_a_401_and_never_a_redirect_to_a_login_page()
    {
        // This is an API, not a website: a 302 would be a different disclosure and would break
        // any client that follows redirects.
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/api/v1/admin/polls");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    private static string SubstituteRouteParameters(string pattern) =>
        Regex.Replace(pattern, @"\{[^}]+\}", "0199a000-0000-7000-8000-000000000000");
}
