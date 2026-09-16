using System.Net;
using System.Net.Http.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 009 FR-050: while maintenance is on, the creator surface is refused with the notice the
/// participant surface already uses.
/// </summary>
/// <remarks>
/// <b>A guard test, expected to pass on its first run.</b> Nothing in this feature implements the
/// requirement: <c>MaintenanceMiddleware</c> intercepts everything under <c>/api</c> except
/// <c>/api/v1/admin</c> and <c>/api/v1/health</c>, and the creator routes are neither. The prefix
/// is the whole mechanism.
/// <para>
/// It exists because that is only true by a decision somebody could reverse. Mounting the creator
/// group under the admin group would have been defensible-looking - the routes are authorised,
/// after all - and would have handed a bearer link the one exemption the middleware grants, letting
/// Ersteller write into a database that a restore is about to replace. Nothing else in the suite
/// would have noticed (research R-6).
/// </para>
/// </remarks>
public sealed class CreatorMaintenanceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "rundfrage-tests", Guid.NewGuid().ToString("n"));

    public CreatorMaintenanceTests() => Directory.CreateDirectory(_directory);

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

    private static async Task SetMaintenanceAsync(ApiFactory factory, bool enabled)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var response = await admin.PutAsJsonAsync("/api/v1/admin/maintenance", new { enabled });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Reading_the_surface_is_refused_with_the_participants_notice()
    {
        using var factory = new ApiFactory(_directory);
        var (_, token) = await CreatorTestData.NewCreatorAsync(factory, "Wartung Anna");

        await SetMaintenanceAsync(factory, true);

        var response = await factory.CreateClient().GetAsync($"/api/v1/e/{token}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("{\"code\":\"maintenance\"}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Nothing_can_be_created_through_a_link_while_maintenance_is_on()
    {
        // The half that matters. A restore is about to replace every row; a poll created in the
        // window between the operator switching maintenance on and the restore finishing is a poll
        // that vanishes without anybody being told.
        using var factory = new ApiFactory(_directory);
        var (_, token) = await CreatorTestData.NewCreatorAsync(factory, "Wartung Ben");

        await SetMaintenanceAsync(factory, true);

        var client = factory.CreateClient();

        var poll = await client.PostAsJsonAsync($"/api/v1/e/{token}/polls", new
        {
            title = "Waehrend der Wartung",
            days = new[] { "2026-12-01" },
        });
        var wishList = await client.PostAsJsonAsync($"/api/v1/e/{token}/wish-lists", new
        {
            title = "Auch waehrend der Wartung",
            targetDate = "2026-12-24",
            items = new[] { new { name = "Kuchen", wantedCount = 1 } },
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, poll.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, wishList.StatusCode);

        // And nothing was stored. The notice is produced by middleware before routing, so the
        // handler never runs - which is what makes "refused" and "not recorded" the same fact.
        await SetMaintenanceAsync(factory, false);

        var surface = await factory.CreateClient()
            .GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/v1/e/{token}");

        Assert.Empty(surface.GetProperty("polls").EnumerateArray());
        Assert.Empty(surface.GetProperty("wishLists").EnumerateArray());
    }

    [Fact]
    public async Task The_admin_area_keeps_working_so_the_operator_can_switch_it_off_again()
    {
        // The exemption the creator routes deliberately do not share. Without it the operator
        // locks themselves out of the switch that ends the window (005 FR-028).
        using var factory = new ApiFactory(_directory);
        await SetMaintenanceAsync(factory, true);

        var admin = await factory.CreateSignedInClientAsync();
        var creators = await admin.GetAsync("/api/v1/admin/creators");

        Assert.Equal(HttpStatusCode.OK, creators.StatusCode);

        await SetMaintenanceAsync(factory, false);
    }
}
