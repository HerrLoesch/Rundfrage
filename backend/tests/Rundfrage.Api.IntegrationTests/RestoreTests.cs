using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Rundfrage.Api.Data;
using Rundfrage.Api.Retention;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// US3: the system goes back to what a backup holds — including every link (FR-016 to FR-024).
/// </summary>
public class RestoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "rundfrage-tests", Guid.NewGuid().ToString("n"));

    private readonly List<string> _files = [];

    public RestoreTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        foreach (var file in _files)
        {
            BackupFileFixture.TryDelete(file);
        }

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private ApiFactory NewFactory() => new(_directory);

    private static async Task SetMaintenanceAsync(ApiFactory factory, bool enabled)
    {
        var admin = await factory.CreateSignedInClientAsync();
        (await admin.PutAsJsonAsync("/api/v1/admin/maintenance", new { enabled }))
            .EnsureSuccessStatusCode();
    }

    private static async Task<string> CreatePollAsync(ApiFactory factory, string title)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var created = await admin.PostAsJsonAsync("/api/v1/admin/polls", new
        {
            title,
            message = (string?)null,
            days = new[] { "2027-10-01", "2027-10-02" },
        });
        created.EnsureSuccessStatusCode();

        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        return summary.GetProperty("participantToken").GetString()!;
    }

    private async Task<string> BackupAsync()
    {
        var path = await BackupFileFixture.CreateAsync(_directory);
        _files.Add(path);
        return path;
    }

    private static async Task<HttpResponseMessage> RestoreAsync(
        ApiFactory factory, string backupPath, bool confirm = true)
    {
        var admin = await factory.CreateSignedInClientAsync();
        return await admin.PostAsync(
            "/api/v1/admin/restore", BackupFileFixture.ToFormContent(backupPath, confirm));
    }

    [Fact]
    public async Task Is_refused_while_maintenance_mode_is_off()
    {
        // FR-024, and the guard that removes the "participant answers mid-restore" case entirely.
        using var factory = NewFactory();
        await CreatePollAsync(factory, "Vorher");
        var backup = await BackupAsync();

        var response = await RestoreAsync(factory, backup);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("maintenance_required", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Every_participant_link_works_again_afterwards()
    {
        // FR-017 and SC-006 — the reason a backup is the restoration path and the JSON export is
        // not. The tokens are ordinary rows, so replacing every row brings them back.
        using var factory = NewFactory();
        var token = await CreatePollAsync(factory, "Mit Link");
        var dayIds = await ImportTestHelper.DayIdsAsync(factory, token);
        await ImportTestHelper.AnswerAsync(factory, token, "Anna", (dayIds[0], "yes"));

        var backup = await BackupAsync();

        // Something changes after the backup was taken.
        await CreatePollAsync(factory, "Nach der Sicherung");

        await SetMaintenanceAsync(factory, true);
        var response = await RestoreAsync(factory, backup);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await SetMaintenanceAsync(factory, false);

        // The very same link, not a reissued one.
        var view = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{token}");
        Assert.Equal("Mit Link", view.GetProperty("title").GetString());
        Assert.Single(view.GetProperty("responses").EnumerateArray());
    }

    [Fact]
    public async Task A_personal_link_still_revises_its_own_answer_afterwards()
    {
        using var factory = NewFactory();
        var token = await CreatePollAsync(factory, "Persoenlich");
        var dayIds = await ImportTestHelper.DayIdsAsync(factory, token);

        var submitted = await factory.CreateClient().PostAsJsonAsync(
            $"/api/v1/polls/{token}/responses",
            new { displayName = "Anna", answers = new[] { new { dayId = dayIds[0], availability = "yes" } } });
        submitted.EnsureSuccessStatusCode();
        var editToken = (await submitted.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("editToken").GetString()!;

        var backup = await BackupAsync();

        await SetMaintenanceAsync(factory, true);
        (await RestoreAsync(factory, backup)).EnsureSuccessStatusCode();
        await SetMaintenanceAsync(factory, false);

        var own = await factory.CreateClient().GetAsync($"/api/v1/responses/{editToken}");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
    }

    [Fact]
    public async Task Polls_created_after_the_backup_are_gone()
    {
        using var factory = NewFactory();
        await CreatePollAsync(factory, "Vorher");
        var backup = await BackupAsync();
        await CreatePollAsync(factory, "Nachher");

        await SetMaintenanceAsync(factory, true);
        (await RestoreAsync(factory, backup)).EnsureSuccessStatusCode();

        var admin = await factory.CreateSignedInClientAsync();
        var listing = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/polls");
        var titles = listing.EnumerateArray().Select(p => p.GetProperty("title").GetString()).ToArray();

        Assert.Contains("Vorher", titles);
        Assert.DoesNotContain("Nachher", titles);
    }

    [Fact]
    public async Task The_preview_names_what_will_be_lost_without_replacing_anything()
    {
        // FR-018.
        using var factory = NewFactory();
        await CreatePollAsync(factory, "Vorher");
        var backup = await BackupAsync();
        await CreatePollAsync(factory, "Nachher");

        await SetMaintenanceAsync(factory, true);

        var admin = await factory.CreateSignedInClientAsync();
        var response = await admin.PostAsync(
            "/api/v1/admin/restore/preview", BackupFileFixture.ToFormContent(backup, confirm: false));
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, preview.GetProperty("pollsInBackup").GetInt32());
        Assert.Equal(1, preview.GetProperty("pollsLost").GetInt32());

        // Nothing was replaced: both polls are still there.
        var listing = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/polls");
        Assert.Equal(2, listing.EnumerateArray().Count());
    }

    [Fact]
    public async Task A_file_that_is_not_a_backup_is_refused_and_the_data_is_untouched()
    {
        // FR-019 and research R-4: the check reads the upload only, so at the point it can fail
        // the live storage has not been touched.
        using var factory = NewFactory();
        await CreatePollAsync(factory, "Unberuehrt");
        await SetMaintenanceAsync(factory, true);

        var junk = BackupFileFixture.CreateNotABackup();
        _files.Add(junk);

        var response = await RestoreAsync(factory, junk);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("not_a_backup", body.GetProperty("code").GetString());

        var admin = await factory.CreateSignedInClientAsync();
        var listing = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/polls");
        Assert.Single(listing.EnumerateArray());
    }

    [Fact]
    public async Task A_real_database_that_is_not_ours_is_refused()
    {
        // integrity_check alone would pass this. Restoring it would replace every poll with
        // nothing, which is the most expensive way to discover the check was too shallow.
        using var factory = NewFactory();
        await CreatePollAsync(factory, "Unberuehrt");
        await SetMaintenanceAsync(factory, true);

        var foreign = await BackupFileFixture.CreateForeignDatabaseAsync();
        _files.Add(foreign);

        var response = await RestoreAsync(factory, foreign);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var admin = await factory.CreateSignedInClientAsync();
        Assert.Single((await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/polls")).EnumerateArray());
    }

    [Fact]
    public async Task A_restore_without_confirmation_is_refused()
    {
        using var factory = NewFactory();
        await CreatePollAsync(factory, "Vorher");
        var backup = await BackupAsync();
        await SetMaintenanceAsync(factory, true);

        var response = await RestoreAsync(factory, backup, confirm: false);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("confirmation_required", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_restore_does_not_change_maintenance_state()
    {
        // FR-023 and FR-030. The marker lives outside the data a restore replaces, so restoring a
        // backup taken before maintenance began cannot reopen the participant side underneath the
        // operator.
        using var factory = NewFactory();
        await CreatePollAsync(factory, "Vorher");

        // Taken while maintenance is OFF - the case that would switch it off again if the state
        // lived in the database.
        var backup = await BackupAsync();

        await SetMaintenanceAsync(factory, true);
        (await RestoreAsync(factory, backup)).EnsureSuccessStatusCode();

        var admin = await factory.CreateSignedInClientAsync();
        var state = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/maintenance");

        Assert.True(state.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task The_uploaded_backup_is_deleted_after_a_restore_and_after_a_preview()
    {
        // FR-005a and SC-007a. The upload carries every link in the system.
        using var factory = NewFactory();
        await CreatePollAsync(factory, "Vorher");
        var backup = await BackupAsync();
        await SetMaintenanceAsync(factory, true);

        var before = TempUploads();

        var admin = await factory.CreateSignedInClientAsync();
        await admin.PostAsync("/api/v1/admin/restore/preview", BackupFileFixture.ToFormContent(backup, false));
        (await RestoreAsync(factory, backup)).EnsureSuccessStatusCode();

        Assert.Equal(before, TempUploads());
    }

    private static int TempUploads() =>
        Directory.GetFiles(Path.GetTempPath(), "rundfrage-upload-*").Length;

    [Fact]
    public async Task Polls_already_past_retention_are_restored_and_named()
    {
        // FR-016a: a restore reproduces the backup rather than filtering it, and says which polls
        // the next sweep will remove.
        using var factory = NewFactory();

        // A poll whose days have passed cannot be created through the API, so it is written the
        // way a backup taken months ago would hold it.
        await CreatePollAsync(factory, "Abgelaufen");
        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            StorageLocation.ConnectionStringFor(_directory)))
        {
            await connection.OpenAsync();
            StorageSetup.Apply(connection);
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Polls SET RetentionDeadline = $past";
            command.Parameters.AddWithValue("$past", DateTime.UtcNow.AddDays(-1).ToString("O"));
            await command.ExecuteNonQueryAsync();
        }

        var backup = await BackupAsync();
        await SetMaintenanceAsync(factory, true);

        var response = await RestoreAsync(factory, backup);
        var summary = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, summary.GetProperty("polls").GetInt32());
        Assert.Contains(
            "Abgelaufen",
            summary.GetProperty("expired").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Restoring_the_same_backup_twice_is_a_safe_no_op()
    {
        using var factory = NewFactory();
        await CreatePollAsync(factory, "Vorher");
        var backup = await BackupAsync();
        await SetMaintenanceAsync(factory, true);

        (await RestoreAsync(factory, backup)).EnsureSuccessStatusCode();
        (await RestoreAsync(factory, backup)).EnsureSuccessStatusCode();

        var admin = await factory.CreateSignedInClientAsync();
        Assert.Single((await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/polls")).EnumerateArray());
    }

    [Fact]
    public async Task The_health_check_stays_green_while_the_storage_is_held_for_a_restore()
    {
        // FR-031 and SC-011, for the restore window rather than the maintenance window. The
        // health check must not touch the storage - if it did, it would go red for the seconds a
        // restore holds it, and the orchestrator would roll the deployment back mid-restore.
        using var factory = NewFactory();
        await CreatePollAsync(factory, "Vorher");
        await SetMaintenanceAsync(factory, true);

        var suspension = factory.Services.GetRequiredService<RetentionSuspension>();
        using var held = await suspension.AcquireAsync(CancellationToken.None);

        var health = await factory.CreateClient().GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task Requires_an_operator_session()
    {
        using var factory = NewFactory();
        await CreatePollAsync(factory, "Vorher");
        var backup = await BackupAsync();

        var response = await factory.CreateClient().PostAsync(
            "/api/v1/admin/restore", BackupFileFixture.ToFormContent(backup));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
