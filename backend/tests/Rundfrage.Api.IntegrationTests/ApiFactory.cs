using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Rundfrage.Api.Data;
using Rundfrage.Api.Http;
using Rundfrage.Api.Security;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>Drives the real host against a chosen data directory with a known operator account.</summary>
public sealed class ApiFactory(string dataDirectory, int? submissionsPerHour = null)
    : WebApplicationFactory<Program>
{
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
        builder.UseSetting(AdminAccount.UserVariable, TestUser);
        builder.UseSetting(AdminAccount.PasswordHashVariable, TestPasswordHash);
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
