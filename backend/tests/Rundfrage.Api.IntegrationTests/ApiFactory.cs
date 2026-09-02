using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>Drives the real host with a chosen connection string.</summary>
public sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    /// <summary>
    /// Reserved, non-routable address: connection attempts hang rather than being refused,
    /// which is what makes "database unreachable" realistic rather than instant.
    /// </summary>
    public const string UnreachableConnection =
        "Host=10.255.255.1;Port=5432;Database=rundfrage;Username=rundfrage;Password=irrelevant;Timeout=2";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.UseSetting("ConnectionStrings:Default", connectionString);
}
