using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Rundfrage.Api.Data;
using Rundfrage.Api.Security;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// US2: while maintenance is on, participants see a notice and nothing else (FR-025 to FR-034).
/// </summary>
public class MaintenanceModeTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "rundfrage-tests", Guid.NewGuid().ToString("n"));

    public MaintenanceModeTests() => Directory.CreateDirectory(_directory);

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

    private ApiFactory NewFactory() => new(_directory);

    private static async Task SetMaintenanceAsync(ApiFactory factory, bool enabled)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var response = await admin.PutAsJsonAsync("/api/v1/admin/maintenance", new { enabled });
        response.EnsureSuccessStatusCode();
    }

    private async Task<(string Token, Guid[] DayIds)> CreatePollAsync(ApiFactory factory)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var created = await admin.PostAsJsonAsync("/api/v1/admin/polls", new
        {
            title = "Grillabend",
            message = "Wer kann wann?",
            days = new[] { "2027-08-14", "2027-08-15" },
        });
        created.EnsureSuccessStatusCode();

        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        var token = summary.GetProperty("participantToken").GetString()!;

        return (token, await ImportTestHelper.DayIdsAsync(factory, token));
    }

    [Fact]
    public async Task A_poll_link_answers_with_the_notice_and_no_poll_content()
    {
        // FR-026: not the title, not the message, not the days, not the results.
        using var factory = NewFactory();
        var (token, _) = await CreatePollAsync(factory);

        await SetMaintenanceAsync(factory, true);

        var response = await factory.CreateClient().GetAsync($"/api/v1/polls/{token}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("Grillabend", body);
        Assert.DoesNotContain("Wer kann wann?", body);
        Assert.DoesNotContain("2027-08-14", body);
    }

    [Fact]
    public async Task A_personal_link_answers_with_the_notice_and_leaves_the_answer_untouched()
    {
        using var factory = NewFactory();
        var (token, dayIds) = await CreatePollAsync(factory);

        var submitted = await factory.CreateClient().PostAsJsonAsync(
            $"/api/v1/polls/{token}/responses",
            new { displayName = "Anna", answers = new[] { new { dayId = dayIds[0], availability = "yes" } } });
        submitted.EnsureSuccessStatusCode();
        var accepted = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        var editToken = accepted.GetProperty("editToken").GetString()!;

        await SetMaintenanceAsync(factory, true);

        var during = await factory.CreateClient().GetAsync($"/api/v1/responses/{editToken}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, during.StatusCode);
        Assert.DoesNotContain("Anna", await during.Content.ReadAsStringAsync());

        await SetMaintenanceAsync(factory, false);

        var after = await factory.CreateClient().GetAsync($"/api/v1/responses/{editToken}");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Contains("Anna", await after.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_notice_is_identical_for_a_real_token_and_an_unknown_one()
    {
        // FR-032 and 002 SC-012: maintenance must not become an existence oracle. A different
        // answer for a real poll would tell an outsider that a poll is behind that link.
        using var factory = NewFactory();
        var (token, _) = await CreatePollAsync(factory);

        await SetMaintenanceAsync(factory, true);

        var real = await factory.CreateClient().GetAsync($"/api/v1/polls/{token}");
        var invented = await factory.CreateClient().GetAsync($"/api/v1/polls/{CapabilityToken.Mint()}");

        Assert.Equal(real.StatusCode, invented.StatusCode);
        Assert.Equal(
            await real.Content.ReadAsStringAsync(),
            await invented.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task No_answer_is_accepted_and_none_is_confirmed()
    {
        // FR-027. The half that matters: a participant is never told an answer was saved when it
        // was not.
        using var factory = NewFactory();
        var (token, dayIds) = await CreatePollAsync(factory);

        await SetMaintenanceAsync(factory, true);

        var submitted = await factory.CreateClient().PostAsJsonAsync(
            $"/api/v1/polls/{token}/responses",
            new { displayName = "Bert", answers = new[] { new { dayId = dayIds[0], availability = "yes" } } });

        Assert.False(submitted.IsSuccessStatusCode);

        await SetMaintenanceAsync(factory, false);

        var view = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{token}");
        var names = view.GetProperty("responses").EnumerateArray()
            .Select(r => r.GetProperty("displayName").GetString()).ToArray();

        Assert.DoesNotContain("Bert", names);
    }

    [Fact]
    public async Task The_admin_area_keeps_working_including_switching_it_back_off()
    {
        // FR-028. Without this the operator locks themselves out of their own maintenance window.
        using var factory = NewFactory();
        await CreatePollAsync(factory);
        await SetMaintenanceAsync(factory, true);

        var admin = await factory.CreateSignedInClientAsync();

        var listing = await admin.GetAsync("/api/v1/admin/polls");
        Assert.Equal(HttpStatusCode.OK, listing.StatusCode);

        var off = await admin.PutAsJsonAsync("/api/v1/admin/maintenance", new { enabled = false });
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
    }

    [Fact]
    public async Task Signing_in_still_works_while_maintenance_is_on()
    {
        using var factory = NewFactory();
        await SetMaintenanceAsync(factory, true);

        var client = factory.CreateClient();
        var signIn = await client.PostAsJsonAsync(
            "/api/v1/admin/session",
            new { user = ApiFactory.TestUser, password = ApiFactory.TestPassword });

        // 204, as the sign-in route has always answered - asserted as "succeeded" rather than as
        // one exact code, because the point here is that maintenance did not intercept it.
        Assert.True(signIn.IsSuccessStatusCode, $"sign-in answered {signIn.StatusCode} during maintenance");
    }

    [Fact]
    public async Task The_health_check_still_reports_healthy()
    {
        // FR-031. Maintenance is a deliberate state, not a fault. Reporting it as one would have
        // the orchestrator replace or roll back the deployment mid-maintenance.
        using var factory = NewFactory();
        await SetMaintenanceAsync(factory, true);

        var response = await factory.CreateClient().GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Maintenance_state_survives_a_restart()
    {
        // FR-029 and SC-010. A redeploy must not silently reopen the participant side.
        using (var first = NewFactory())
        {
            await SetMaintenanceAsync(first, true);
        }

        using var second = NewFactory();
        var admin = await second.CreateSignedInClientAsync();
        var state = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/maintenance");

        Assert.True(state.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Switching_it_on_and_off_alters_no_poll_or_response()
    {
        // FR-034.
        using var factory = NewFactory();
        var (token, dayIds) = await CreatePollAsync(factory);
        await ImportTestHelper.AnswerAsync(factory, token, "Anna", (dayIds[0], "yes"));

        var before = await factory.CreateClient().GetStringAsync($"/api/v1/polls/{token}");

        await SetMaintenanceAsync(factory, true);
        await SetMaintenanceAsync(factory, false);

        var after = await factory.CreateClient().GetStringAsync($"/api/v1/polls/{token}");

        Assert.Equal(before, after);
    }

    [Fact]
    public async Task The_marker_lives_beside_the_storage_and_not_inside_it()
    {
        // FR-030 in its concrete form. This is what makes a restore unable to switch it off:
        // a restore replaces the database file and its companions, and this is neither.
        using var factory = NewFactory();
        await SetMaintenanceAsync(factory, true);

        Assert.True(File.Exists(StorageLocation.MaintenanceMarkerIn(_directory)));
        Assert.NotEqual(
            StorageLocation.FileIn(_directory),
            StorageLocation.MaintenanceMarkerIn(_directory));
    }
}
