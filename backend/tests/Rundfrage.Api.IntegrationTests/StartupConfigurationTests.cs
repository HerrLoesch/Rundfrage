using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Rundfrage.Api.Security;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// SC-015 and FR-045. A missing credential must stop the application at startup, not at the
/// first sign-in attempt, so a misconfigured deployment fails loudly rather than quietly.
/// </summary>
public class StartupConfigurationTests
{
    private sealed class BareFactory(string? user, string? hash) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Default", ApiFactory.UnreachableDirectory);
            builder.UseSetting(AdminAccount.UserVariable, user);
            builder.UseSetting(AdminAccount.PasswordHashVariable, hash);
        }
    }

    [Theory]
    [InlineData(null, "pbkdf2-sha256:600000:abc:def")]
    [InlineData("", "pbkdf2-sha256:600000:abc:def")]
    [InlineData("admin", null)]
    [InlineData("admin", "")]
    public void Refuses_to_start_without_both_admin_variables(string? user, string? hash)
    {
        using var factory = new BareFactory(user, hash);

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("ADMIN_", exception.ToString());
    }

    [Fact]
    public void Starts_when_both_are_present()
    {
        using var factory = new BareFactory("admin", PasswordHash.Generate("whatever"));

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.Null(exception);
    }

    [Fact]
    public async Task A_plaintext_password_in_the_hash_variable_never_signs_anyone_in()
    {
        // SC-015: the deployment configuration must contain no usable plaintext password.
        using var factory = new BareFactory("admin", "hunter2");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/session", new { user = "admin", password = "hunter2" });

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
