using System.Net.Http.Json;
using System.Text.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// FR-043a and FR-043b, SC-023 and SC-024: security-relevant events must be visible, and none of
/// them may carry a credential, a name, an answer, a token or a request source.
/// </summary>
/// <remarks>
/// <para>
/// <b>This replaces a set of tests that verified nothing.</b> They created a recording logger,
/// called <c>logger.LogWarning("Sign-in failed")</c> <i>from the test itself</i>, and then
/// asserted that the string the test had just written contained no password. The logger was
/// never handed to any production code, so the application could have logged the password in
/// full and every test would still have passed.
/// </para>
/// <para>
/// These drive the real endpoints and read what the application actually wrote to standard
/// output through Serilog - the same path an operator reads with <c>docker compose logs</c>.
/// Console output is process-global, so this collection runs without parallelism.
/// </para>
/// </remarks>
[Collection(nameof(ConsoleCapturingCollection))]
public class AdminLoggingTests
{
    private const string WrongPassword = "a-distinctive-wrong-password";

    /// <summary>Runs the host with stdout captured, and returns everything it logged.</summary>
    private static async Task<string> CapturingAsync(Func<HttpClient, ApiFactory, Task> exercise)
    {
        var captured = new StringWriter();
        var original = Console.Out;
        Console.SetOut(captured);

        try
        {
            using var factory = new ApiFactory(ApiFactory.UnreachableConnection);
            var client = factory.CreateClient();
            await exercise(client, factory);
            await Task.Delay(50); // let the sink drain
        }
        finally
        {
            Console.SetOut(original);
        }

        return captured.ToString();
    }

    private static IEnumerable<JsonElement> Entries(string log)
    {
        foreach (var line in log.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            JsonDocument? parsed = null;
            try
            {
                parsed = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                // Not every line of container output is a Serilog entry.
            }

            if (parsed is not null)
            {
                yield return parsed.RootElement.Clone();
            }
        }
    }

    private static IEnumerable<string> Messages(string log) =>
        Entries(log)
            .Where(e => e.TryGetProperty("@mt", out _))
            .Select(e => e.GetProperty("@mt").GetString() ?? string.Empty);

    [Fact]
    public async Task A_failed_sign_in_is_logged()
    {
        var log = await CapturingAsync(async (client, _) =>
            await client.PostAsJsonAsync(
                "/api/v1/admin/session", new { user = ApiFactory.TestUser, password = WrongPassword }));

        Assert.Contains(Messages(log), m => m.Contains("Sign-in failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_failed_sign_in_logs_neither_the_password_nor_the_user_name()
    {
        var log = await CapturingAsync(async (client, _) =>
            await client.PostAsJsonAsync(
                "/api/v1/admin/session", new { user = ApiFactory.TestUser, password = WrongPassword }));

        Assert.DoesNotContain(WrongPassword, log);
        Assert.DoesNotContain(ApiFactory.TestUser, log);
        Assert.DoesNotContain(ApiFactory.TestPassword, log);
    }

    [Fact]
    public async Task A_successful_sign_in_is_logged_and_carries_no_credential()
    {
        var log = await CapturingAsync(async (client, _) =>
            await client.PostAsJsonAsync(
                "/api/v1/admin/session",
                new { user = ApiFactory.TestUser, password = ApiFactory.TestPassword }));

        Assert.Contains(Messages(log), m => m.Contains("Sign-in succeeded", StringComparison.Ordinal));
        Assert.DoesNotContain(ApiFactory.TestPassword, log);
        Assert.DoesNotContain("pbkdf2", log, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_lockout_is_logged()
    {
        var log = await CapturingAsync(async (client, _) =>
        {
            for (var i = 0; i < 6; i++)
            {
                await client.PostAsJsonAsync(
                    "/api/v1/admin/session", new { user = ApiFactory.TestUser, password = WrongPassword });
            }
        });

        Assert.Contains(Messages(log), m => m.Contains("locked", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task No_entry_carries_a_property_named_for_participant_data()
    {
        // The property names are the contract. A placeholder called Title, DisplayName, Answer,
        // Token or Password would be the leak - and it would be invisible until it fired.
        var log = await CapturingAsync(async (client, _) =>
        {
            await client.PostAsJsonAsync(
                "/api/v1/admin/session", new { user = ApiFactory.TestUser, password = WrongPassword });
            await client.PostAsJsonAsync(
                "/api/v1/admin/session",
                new { user = ApiFactory.TestUser, password = ApiFactory.TestPassword });
        });

        var forbidden = new[] { "password", "displayname", "answer", "token", "remoteaddress", "useragent" };

        foreach (var entry in Entries(log))
        {
            foreach (var property in entry.EnumerateObject())
            {
                var name = property.Name.ToLowerInvariant();
                Assert.DoesNotContain(name, forbidden);
            }
        }
    }
}

[CollectionDefinition(nameof(ConsoleCapturingCollection), DisableParallelization = true)]
public sealed class ConsoleCapturingCollection;
