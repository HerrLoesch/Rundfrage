using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 009 FR-051 and FR-052: Ersteller travel in the backup, and the preview says how many a restore
/// would destroy.
/// </summary>
/// <remarks>
/// Two different claims, and the suite makes both. <b>The preview counts</b> - or the operator
/// confirms a restore against a statement that says nothing about the hundred links they have
/// issued. <b>The round trip works</b> - or the counts are a promise the restore does not keep.
/// </remarks>
public sealed class CreatorRestoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "rundfrage-tests", Guid.NewGuid().ToString("n"));

    public CreatorRestoreTests() => Directory.CreateDirectory(_directory);

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

    private static async Task SetMaintenanceAsync(HttpClient admin, bool enabled) =>
        (await admin.PutAsJsonAsync("/api/v1/admin/maintenance", new { enabled }))
            .EnsureSuccessStatusCode();

    private static MultipartFormDataContent Upload(byte[] backup)
    {
        var content = new ByteArrayContent(backup);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { content, "file", "rundfrage.db" } };
    }

    [Fact]
    public async Task The_preview_counts_the_Ersteller_a_restore_would_destroy()
    {
        using var factory = new ApiFactory(_directory);

        // A backup holding one Ersteller.
        await CreatorTestData.NewCreatorAsync(factory, "Im Backup");
        var admin = await factory.CreateSignedInClientAsync();
        var backup = await (await admin.GetAsync("/api/v1/admin/backup")).Content.ReadAsByteArrayAsync();

        // Two more issued afterwards - the links that a restore would silently invalidate.
        await CreatorTestData.NewCreatorAsync(factory, "Danach eins");
        await CreatorTestData.NewCreatorAsync(factory, "Danach zwei");

        // Previewing requires maintenance mode, as restoring does (005 FR-024): the storage is
        // taken exclusively for the read.
        await SetMaintenanceAsync(admin, true);

        using var upload = Upload(backup);
        var preview = await admin.PostAsync("/api/v1/admin/restore/preview", upload);
        preview.EnsureSuccessStatusCode();

        var body = await preview.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetProperty("creatorsInBackup").GetInt32());

        // FR-052: two would be lost, and the operator is told so before confirming.
        Assert.Equal(2, body.GetProperty("creatorsLost").GetInt32());

        await SetMaintenanceAsync(admin, false);
    }

    [Fact]
    public async Task A_backup_taken_before_this_feature_still_previews_and_counts_zero()
    {
        // data-model section 9. Such a file has no Creators table at all, and it is still a
        // perfectly restorable backup - counting zero is the truth about it, where failing to read
        // it would refuse a valid restore.
        using var factory = new ApiFactory(_directory);
        var admin = await factory.CreateSignedInClientAsync();

        var backup = await (await admin.GetAsync("/api/v1/admin/backup")).Content.ReadAsByteArrayAsync();

        // Drop the table from the copy, which is what a pre-009 file looks like.
        var doctored = Path.Combine(_directory, "pre-009.db");
        await File.WriteAllBytesAsync(doctored, backup);

        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            $"Data Source={doctored};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var drop = connection.CreateCommand();
            drop.CommandText = "DROP TABLE IF EXISTS Creators";
            await drop.ExecuteNonQueryAsync();
        }

        await SetMaintenanceAsync(admin, true);

        using var upload = Upload(await File.ReadAllBytesAsync(doctored));
        var preview = await admin.PostAsync("/api/v1/admin/restore/preview", upload);

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);

        var body = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("creatorsInBackup").GetInt32());

        await SetMaintenanceAsync(admin, false);
    }

    [Fact]
    public async Task A_restored_link_authorises_again_against_the_content_it_owned()
    {
        // FR-051, and the claim the counts above would otherwise only imply. The backup is a file
        // copy, so the Ersteller and its token travel with it - but "the row is there" and "the
        // link works" are different facts, and only the second one matters to the person holding
        // the link.
        using var factory = new ApiFactory(_directory, creatorWritesPerHour: 1000);

        var (id, token) = await CreatorTestData.NewCreatorAsync(factory, "Wiederhergestellt");
        var anna = factory.CreateClient();
        var pollId = await CreatorTestData.NewPollAsync(anna, token, "Termin vor der Sicherung");

        var admin = await factory.CreateSignedInClientAsync();
        var backup = await (await admin.GetAsync("/api/v1/admin/backup")).Content.ReadAsByteArrayAsync();

        // Destroy the Ersteller and everything it owned.
        (await admin.DeleteAsync($"/api/v1/admin/creators/{id}")).EnsureSuccessStatusCode();
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await factory.CreateClient().GetAsync($"/api/v1/e/{token}")).StatusCode);

        // Restore.
        await SetMaintenanceAsync(admin, true);
        using var upload = Upload(backup);
        upload.Add(new StringContent("true"), "confirm");
        var restored = await admin.PostAsync("/api/v1/admin/restore", upload);
        restored.EnsureSuccessStatusCode();
        await SetMaintenanceAsync(admin, false);

        // The same link works again, and reaches the same poll.
        var surface = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/e/{token}");

        Assert.Equal("Wiederhergestellt", surface.GetProperty("name").GetString());
        Assert.Equal(
            pollId,
            surface.GetProperty("polls").EnumerateArray().Single().GetProperty("id").GetGuid());
    }
}
