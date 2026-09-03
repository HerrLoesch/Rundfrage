using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Rundfrage.Api.Data;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// FR-003, FR-003a, FR-003b, SC-005: a backup that can actually be restored, and the reason the
/// obvious alternative cannot.
/// </summary>
public class BackupTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private static async Task<(string Token, Guid[] DayIds)> CreatePollAsync(ApiFactory factory)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var created = await admin.PostAsJsonAsync(
            "/api/v1/admin/polls", new { title = "Sicherung", days = new[] { "2026-11-22" } });

        var token = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("participantToken").GetString()!;

        var view = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{token}");
        var dayIds = view.GetProperty("days").EnumerateArray()
            .Select(d => d.GetProperty("id").GetGuid()).ToArray();

        return (token, dayIds);
    }

    /// <summary>Answers continuously until told to stop, so the backup is taken mid-write.</summary>
    private static Task WhileAnsweringAsync(
        ApiFactory factory, string token, Guid dayId, CancellationToken stop)
    {
        return Task.Run(async () =>
        {
            var client = factory.CreateClient();

            for (var i = 0; !stop.IsCancellationRequested; i++)
            {
                await client.PostAsJsonAsync($"/api/v1/polls/{token}/responses", new
                {
                    displayName = $"Laufend {i}",
                    answers = new[] { new { dayId, availability = "yes" } },
                });
            }
        }, CancellationToken.None);
    }

    /// <summary>
    /// Copies the bytes the way <c>cp</c> does, sharing the file with whoever else has it open.
    /// </summary>
    /// <remarks>
    /// <see cref="File.Copy(string, string)"/> will not do: it asks for a share mode the running
    /// system does not grant, so the copy fails outright. That failure is an artefact of this
    /// platform's file API, not of the operator's situation - and a test that stopped there
    /// would never reach the thing worth demonstrating.
    /// </remarks>
    private static async Task CopyLikeCpAsync(string source, string destination)
    {
        await using var input = new FileStream(
            source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
        await input.CopyToAsync(output);
    }

    private static async Task<T> ReadFromAsync<T>(string file, string sql, Func<SqliteDataReader, T> read)
    {
        await using var connection = new SqliteConnection($"Data Source={file};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return read((SqliteDataReader)reader);
    }

    [Fact]
    public async Task A_backup_taken_while_answers_are_arriving_is_complete_and_stands_alone()
    {
        // SC-005. The download is written to a file and opened as a database in its own right -
        // no journal, no shared-memory companion, nothing from the original directory.
        var directory = Path.Combine(storage.DataDirectory, "live");
        Directory.CreateDirectory(directory);

        using var factory = new ApiFactory(directory, submissionsPerHour: 10_000);
        var (token, dayIds) = await CreatePollAsync(factory);

        // One answer recorded before anything else, so "the backup contains answers" is a fact
        // rather than a race against however fast this machine happens to be today. Waiting a
        // fixed 300 ms for the writer to land its first answer is exactly the kind of assumption
        // that passes here and fails on a loaded build agent.
        var first = await factory.CreateClient().PostAsJsonAsync(
            $"/api/v1/polls/{token}/responses",
            new { displayName = "Vorab", answers = new[] { new { dayId = dayIds[0], availability = "yes" } } });
        first.EnsureSuccessStatusCode();

        using var stopWriting = new CancellationTokenSource();
        var writing = WhileAnsweringAsync(factory, token, dayIds[0], stopWriting.Token);

        var admin = await factory.CreateSignedInClientAsync();
        var download = await admin.GetAsync("/api/v1/admin/backup");

        await stopWriting.CancelAsync();
        await writing;

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Contains("attachment", download.Content.Headers.ContentDisposition?.ToString() ?? "");

        var backupFile = Path.Combine(Path.GetTempPath(), $"restored-{Guid.NewGuid():n}.db");
        await File.WriteAllBytesAsync(backupFile, await download.Content.ReadAsByteArrayAsync());

        try
        {
            var tables = await ReadFromAsync(
                backupFile,
                "SELECT group_concat(name) FROM sqlite_master WHERE type = 'table'",
                r => r.GetString(0));

            Assert.Contains("Polls", tables);
            Assert.Contains("CandidateDays", tables);
            Assert.Contains("Responses", tables);
            Assert.Contains("DayAnswers", tables);

            // The poll and at least one of the answers written while the copy was being taken.
            var responses = await ReadFromAsync(
                backupFile, "SELECT COUNT(*) FROM Responses", r => r.GetInt64(0));
            Assert.True(responses > 0, "the backup must contain the answers recorded before it was taken");

            // Internally consistent: every answer belongs to a response and a day that are also
            // in the copy. A half-written response would show up here as an orphan.
            var orphans = await ReadFromAsync(
                backupFile,
                """
                SELECT COUNT(*) FROM DayAnswers a
                LEFT JOIN Responses r ON r.Id = a.ResponseId
                LEFT JOIN CandidateDays d ON d.Id = a.CandidateDayId
                WHERE r.Id IS NULL OR d.Id IS NULL
                """,
                r => r.GetInt64(0));
            Assert.Equal(0, orphans);
        }
        finally
        {
            File.Delete(backupFile);
        }
    }

    [Fact]
    public async Task A_hand_copy_taken_while_the_system_runs_is_not_a_faithful_copy()
    {
        // FR-003b, and the measurement it rests on. The requirement is scoped to "while the
        // system is running", and that scope is the whole point: with every connection closed
        // the engine folds the companion file back into the main one, so a copy taken from a
        // stopped system is fine. It is the running system that makes `cp` dangerous - and a
        // running system is exactly when someone reaches for it, because they did not want to
        // interrupt anyone.
        var directory = Path.Combine(storage.DataDirectory, "handcopy");
        Directory.CreateDirectory(directory);

        using var factory = new ApiFactory(directory, submissionsPerHour: 10_000);
        var (token, dayIds) = await CreatePollAsync(factory);

        // Stands in for a running system that is in the middle of reading - an export, or a
        // results page. The open read snapshot is what pins recent commits in the companion
        // file: the engine may not fold them into the main file past a reader that could still
        // need the older view.
        //
        // Chosen because it makes the condition certain rather than likely. An idle open
        // connection produces the same danger, but only until the engine happens to fold the
        // companion file back in, and a test that waits for a coincidence is a test that fails
        // on someone else's machine.
        await using var systemIsRunning = new SqliteConnection(
            StorageLocation.ConnectionStringFor(directory));
        await systemIsRunning.OpenAsync();
        await using var openReader = systemIsRunning.BeginTransaction(deferred: true);
        await using (var pin = systemIsRunning.CreateCommand())
        {
            pin.CommandText = "SELECT COUNT(*) FROM Responses";
            pin.Transaction = (SqliteTransaction)openReader;
            await pin.ExecuteScalarAsync();
        }

        var client = factory.CreateClient();
        for (var i = 0; i < 20; i++)
        {
            await client.PostAsJsonAsync($"/api/v1/polls/{token}/responses", new
            {
                displayName = $"Kopie {i}",
                answers = new[] { new { dayId = dayIds[0], availability = "yes" } },
            });
        }

        // Exactly what an operator would do: copy the one file whose name they recognise.
        // Taken before anything else touches the storage - opening and closing another
        // connection here would be a checkpoint, and the copy would then look fine for a reason
        // that has nothing to do with the operator's situation.
        var handCopy = Path.Combine(Path.GetTempPath(), $"handcopy-{Guid.NewGuid():n}.db");
        await CopyLikeCpAsync(StorageLocation.FileIn(directory), handCopy);

        var original = await ReadFromAsync(
            StorageLocation.FileIn(directory), "SELECT COUNT(*) FROM Responses", r => r.GetInt64(0));
        Assert.Equal(20, original);

        try
        {
            long? copied = null;
            Exception? failure = null;

            try
            {
                copied = await ReadFromAsync(
                    handCopy, "SELECT COUNT(*) FROM Responses", r => r.GetInt64(0));
            }
            catch (SqliteException ex)
            {
                failure = ex;
            }

            // Either it cannot be read at all - in this journal mode even the schema can still
            // be in the companion file - or it is missing answers the original has. It cannot
            // have more. Both outcomes carry the same lesson, so the assertion covers both
            // rather than pinning whichever one happens on this machine today.
            Assert.True(
                failure is not null || copied < original,
                $"a hand copy taken while the system was running reported {copied} of {original} "
                + "answers and opened cleanly; if that is now reliable, FR-003b needs revisiting "
                + "rather than this test relaxing");
        }
        finally
        {
            File.Delete(handCopy);
        }
    }

    [Fact]
    public async Task A_backup_is_refused_without_a_creator_session()
    {
        using var factory = new ApiFactory(storage.DataDirectory);

        var response = await factory.CreateClient().GetAsync("/api/v1/admin/backup");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Nothing_produced_for_a_download_outlives_the_request()
    {
        // FR-021: an export or a backup is produced on demand and never kept.
        var before = Directory.GetFiles(Path.GetTempPath(), "rundfrage-backup-*.db").Length;

        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var download = await admin.GetAsync("/api/v1/admin/backup");
        await download.Content.ReadAsByteArrayAsync();
        download.Dispose();

        var after = Directory.GetFiles(Path.GetTempPath(), "rundfrage-backup-*.db").Length;
        Assert.Equal(before, after);
    }
}
