using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Rundfrage.Api.Security;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>Drives the real host with a chosen connection string and a known operator account.</summary>
public sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    /// <summary>
    /// Reserved, non-routable address: connection attempts hang rather than being refused,
    /// which is what makes "database unreachable" realistic rather than instant.
    /// </summary>
    public const string UnreachableConnection =
        "Host=10.255.255.1;Port=5432;Database=rundfrage;Username=rundfrage;Password=irrelevant;Timeout=2";

    public const string TestUser = "test-operator";
    public const string TestPassword = "test-password-not-used-anywhere-real";

    /// <summary>
    /// Hashed at run time rather than pasted in as a literal, so the tests exercise the same
    /// generate-then-verify path an operator uses (FR-045a).
    /// </summary>
    private static readonly string TestPasswordHash = PasswordHash.Generate(TestPassword);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", connectionString);
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
