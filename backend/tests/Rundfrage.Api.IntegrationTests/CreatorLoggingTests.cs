using System.Net.Http.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 009 FR-037, FR-040b and SC-013: what the log may say about an Ersteller, and what it may not.
/// </summary>
/// <remarks>
/// Three rules, and the third is the one that looks like an omission until it is read as a
/// decision:
/// <list type="bullet">
/// <item><b>Never a token.</b> It is a credential in a URL; a log line carrying one turns
/// <c>docker compose logs</c> into a list of working links (FR-037).</item>
/// <item><b>Never a name.</b> Operator-written rather than participant data, so it would be
/// defensible - and it is excluded anyway, because a log line naming a person is a step the feature
/// does not need and SC-013 is easier to assert when the rule has no exceptions (research R-13).
/// </item>
/// <item><b>Never who acted.</b> There is no audit trail, deliberately: FR-040b refuses one, and a
/// log line recording that the operator edited an Ersteller's list would be one by another
/// name.</item>
/// </list>
/// Like the other logging suites, this drives the real endpoints and reads what the application
/// actually wrote to standard output - the same path an operator reads.
/// </remarks>
[Collection(nameof(ConsoleCapturingCollection))]
public sealed class CreatorLoggingTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "rundfrage-tests", Guid.NewGuid().ToString("n"));

    public CreatorLoggingTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private const string DistinctiveName = "Gwendolyn-Unverwechselbar";

    /// <summary>
    /// Exercises every creator route, including its refusals, with stdout captured.
    /// </summary>
    private async Task<(string Log, string Token)> ExerciseEverythingAsync()
    {
        var captured = new StringWriter();
        var original = Console.Out;
        string token;

        Console.SetOut(captured);
        try
        {
            using var factory = new ApiFactory(_directory, creatorWritesPerHour: 1000);

            (_, token) = await CreatorTestData.NewCreatorAsync(factory, DistinctiveName);
            var client = factory.CreateClient();

            // The happy paths.
            var pollId = await CreatorTestData.NewPollAsync(client, token);
            var listId = await CreatorTestData.NewWishListAsync(client, token);
            await client.GetAsync($"/api/v1/e/{token}");
            await client.GetAsync($"/api/v1/e/{token}/polls/{pollId}");
            await client.GetAsync($"/api/v1/e/{token}/polls/{pollId}/export");
            await client.PatchAsJsonAsync(
                $"/api/v1/e/{token}/wish-lists/{listId}", new { title = "Geändert" });
            await client.DeleteAsync($"/api/v1/e/{token}/polls/{pollId}");
            await client.DeleteAsync($"/api/v1/e/{token}/wish-lists/{listId}");

            // And the refusals, which are where a careless implementation says the most.
            await client.GetAsync("/api/v1/e/abcdefghijklmnopqrstuv");
            await client.GetAsync($"/api/v1/e/{token}/polls/{Guid.CreateVersion7()}");
            await client.PostAsJsonAsync($"/api/v1/e/{token}/polls", new { title = "", days = Array.Empty<string>() });

            // The operator acting on somebody else's content - FR-040b's case.
            var admin = await factory.CreateSignedInClientAsync();
            await admin.GetAsync("/api/v1/admin/creators");

            await Task.Delay(100); // let the sink drain
        }
        finally
        {
            Console.SetOut(original);
        }

        return (captured.ToString(), token);
    }

    [Fact]
    public async Task No_participant_token_ever_reaches_the_log_either()
    {
        // Written while chasing the creator-token leak, and kept because it found the same defect
        // one layer older: ASP.NET Core attaches RequestPath to every log entry in a request's
        // scope, so feature 002's participant token and feature 008's claim token were in the log
        // too - which 002 FR-043a already forbade (see LoggingSetup's RequestPathIsACredential).
        var captured = new StringWriter();
        var original = Console.Out;
        string participantToken;

        Console.SetOut(captured);
        try
        {
            using var factory = new ApiFactory(_directory);
            var admin = await factory.CreateSignedInClientAsync();
            var created = await admin.PostAsJsonAsync("/api/v1/admin/polls", new
            {
                title = "Fuer das Protokoll",
                days = new[] { "2026-12-01" },
            });
            var body = await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            participantToken = body.GetProperty("participantToken").GetString()!;

            await factory.CreateClient().GetAsync($"/api/v1/polls/{participantToken}");
            await Task.Delay(100);
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.DoesNotContain(participantToken, captured.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_creator_token_ever_reaches_the_log()
    {
        var (log, token) = await ExerciseEverythingAsync();

        Assert.DoesNotContain(token, log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_creator_name_ever_reaches_the_log()
    {
        var (log, _) = await ExerciseEverythingAsync();

        Assert.DoesNotContain(DistinctiveName, log, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Nothing_records_who_performed_an_action()
    {
        // FR-040b. The operator may change an Ersteller's content without notice, and the system
        // deliberately keeps no record of having done so - a notification would need a contact
        // detail the Creator row does not hold, and an audit trail stores more than the feature
        // needs (Principle IV).
        var (log, _) = await ExerciseEverythingAsync();

        foreach (var giveaway in new[] { "actor", "performedBy", "OnBehalfOf", "audit" })
        {
            Assert.DoesNotContain(giveaway, log, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task No_request_source_reaches_the_log_through_the_new_rate_limit()
    {
        // FR-036d. The creator budget partitions by request source, and that source may be used
        // transiently and written nowhere - not to storage, and not here.
        var (log, _) = await ExerciseEverythingAsync();

        Assert.DoesNotContain(ApiFactory.ConnectingAddress.ToString(), log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_events_that_matter_are_still_visible_by_identifier()
    {
        // The counterweight. A log that says nothing satisfies every rule above and is useless:
        // an operator must still be able to see that an Ersteller was created and that its link
        // was taken away, which is why those lines carry the identifier.
        var (log, _) = await ExerciseEverythingAsync();

        Assert.Contains("Ersteller created", log, StringComparison.Ordinal);
    }
}
