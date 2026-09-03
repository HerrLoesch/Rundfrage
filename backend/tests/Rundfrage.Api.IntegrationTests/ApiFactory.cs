using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Rundfrage.Api.Data;
using Rundfrage.Api.Http;
using Rundfrage.Api.Security;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>Drives the real host against a chosen data directory with a known operator account.</summary>
public sealed class ApiFactory(
    string dataDirectory, int? submissionsPerHour = null, int? trustedProxies = null)
    : WebApplicationFactory<Program>
{
    /// <summary>
    /// The address every request appears to come from, standing in for the one Kestrel would
    /// report. Behind a proxy that is the proxy's address, which is the whole problem the
    /// forwarded headers exist to solve.
    /// </summary>
    public static readonly IPAddress ConnectingAddress = IPAddress.Parse("10.0.0.2");

    /// <summary>
    /// TestServer leaves the connection's remote address null, and the forwarded-headers
    /// middleware only rewrites an address that is already there - so without this the proxy
    /// tests would pass or fail for a reason that has nothing to do with the application.
    /// </summary>
    private sealed class ConnectedFromAnAddress : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, following) =>
            {
                context.Connection.RemoteIpAddress = ConnectingAddress;
                context.Connection.RemotePort = 40404;
                return following(context);
            });

            next(app);
        };
    }

    /// <summary>
    /// A directory that cannot be created or written to. This is what "storage unreachable"
    /// means once storage is a file: not a host that refuses connections, but a path that
    /// cannot be opened.
    /// </summary>
    /// <remarks>
    /// It is a path *underneath a regular file*, which no process can turn into a directory -
    /// not even root, and not on any platform. A merely absent path would not do: the
    /// application creates its directory on first start (FR-004), so an absent path would
    /// quietly succeed and the resilience tests would assert nothing.
    /// </remarks>
    public static readonly string UnreachableDirectory = CreateUnreachablePath();

    private static string CreateUnreachablePath()
    {
        var blocker = Path.Combine(Path.GetTempPath(), $"rundfrage-not-a-directory-{Guid.NewGuid():n}");
        File.WriteAllText(blocker, "This file exists so that the path beneath it cannot be a directory.");
        return Path.Combine(blocker, "data");
    }

    public const string TestUser = "test-operator";
    public const string TestPassword = "test-password-not-used-anywhere-real";

    /// <summary>
    /// Hashed at run time rather than pasted in as a literal, so the tests exercise the same
    /// generate-then-verify path an operator uses (002 FR-045a).
    /// </summary>
    private static readonly string TestPasswordHash = PasswordHash.Generate(TestPassword);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(StorageLocation.DataDirectoryVariable, dataDirectory);

        // Left unset by default so the rate limit stays the ten FR-027a requires and the tests
        // about it keep testing it. Raised only where a test drives more submissions than a
        // person could - concurrency, for instance - and would otherwise be measuring the
        // limiter instead of the thing it means to measure.
        if (submissionsPerHour is { } permits)
        {
            builder.UseSetting(RateLimiting.PermitsVariable, permits.ToString());
        }
        // Left unset by default so the application under test is the direct deployment, in
        // which a forwarded header is nothing but the caller's claim about itself.
        if (trustedProxies is { } proxies)
        {
            builder.UseSetting(ReverseProxy.TrustedProxyCountVariable, proxies.ToString());
        }

        builder.UseSetting(AdminAccount.UserVariable, TestUser);
        builder.UseSetting(AdminAccount.PasswordHashVariable, TestPasswordHash);

        builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter, ConnectedFromAnAddress>());
    }

    /// <summary>A client that has already signed in, for tests about what happens afterwards.</summary>
    public async Task<HttpClient> CreateSignedInClientAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/session", new { user = TestUser, password = TestPassword });

        response.EnsureSuccessStatusCode();
        return client;
    }
}
