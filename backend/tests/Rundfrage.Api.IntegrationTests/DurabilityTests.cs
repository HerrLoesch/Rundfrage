using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Rundfrage.Api.Data;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// FR-012a, SC-007: an answer that was confirmed to its participant is really on disk.
/// </summary>
/// <remarks>
/// <b>What this can and cannot prove.</b> Cutting power to the machine is not something a test
/// can do, so nothing here can observe the difference between a commit that reached the disk and
/// one that reached the operating system's cache. That difference is carried by a setting, and
/// the setting is asserted in <see cref="StorageSettingsTests"/> - which does fail if it is
/// relaxed.
/// <para>
/// What is left for this suite is the half that <em>is</em> observable, and it is not nothing: a
/// confirmed answer is committed rather than merely queued in the application, visible to a
/// connection that shares no state with the one that wrote it, and still there when the host that
/// wrote it goes away without a clean shutdown. Those are the ways an answer could be lost that
/// a test can actually catch.
/// </para>
/// </remarks>
public class DurabilityTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private static async Task<(string Token, Guid[] DayIds)> CreatePollAsync(ApiFactory factory)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var created = await admin.PostAsJsonAsync(
            "/api/v1/admin/polls", new { title = "Haltbarkeit", days = new[] { "2026-11-21" } });

        var token = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("participantToken").GetString()!;

        var view = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{token}");
        var dayIds = view.GetProperty("days").EnumerateArray()
            .Select(d => d.GetProperty("id").GetGuid()).ToArray();

        return (token, dayIds);
    }

    /// <summary>
    /// Counts through a connection this test opens itself, sharing nothing with the application:
    /// not its context, not its connection, not its pool.
    /// </summary>
    private static async Task<long> CountResponsesAsync(string dataDirectory, string displayName)
    {
        await using var connection = new SqliteConnection(
            StorageLocation.ConnectionStringFor(dataDirectory));
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Responses WHERE DisplayName = $name";
        command.Parameters.AddWithValue("$name", displayName);

        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task A_confirmed_answer_is_already_in_storage_when_the_confirmation_arrives()
    {
        const string name = "Bestätigt";
        using var factory = new ApiFactory(storage.DataDirectory);
        var (token, dayIds) = await CreatePollAsync(factory);

        var submitted = await factory.CreateClient().PostAsJsonAsync(
            $"/api/v1/polls/{token}/responses",
            new { displayName = name, answers = new[] { new { dayId = dayIds[0], availability = "yes" } } });

        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);

        // No shutdown, no flush, no waiting: the moment the participant was told "recorded", an
        // independent reader must be able to see it.
        Assert.Equal(1, await CountResponsesAsync(storage.DataDirectory, name));
    }

    [Fact]
    public async Task A_confirmed_answer_survives_the_host_that_recorded_it_disappearing()
    {
        const string name = "Überlebt";
        var directory = Path.Combine(storage.DataDirectory, "abrupt");
        Directory.CreateDirectory(directory);

        // The host is disposed without any application-level cleanup between the confirmation
        // and the disposal - the closest a test can get to the process simply ending.
        using (var factory = new ApiFactory(directory))
        {
            var (token, dayIds) = await CreatePollAsync(factory);

            var submitted = await factory.CreateClient().PostAsJsonAsync(
                $"/api/v1/polls/{token}/responses",
                new { displayName = name, answers = new[] { new { dayId = dayIds[0], availability = "maybe" } } });

            Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        }

        Assert.Equal(1, await CountResponsesAsync(directory, name));
    }
}
