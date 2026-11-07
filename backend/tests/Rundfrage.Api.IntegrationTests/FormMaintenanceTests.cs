using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 010 FR-045: while maintenance is on, the public form route is refused with the participant
/// notice; the admin routes stay reachable, exactly like every other admin surface (research R-8).
/// </summary>
public class FormMaintenanceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "rundfrage-tests", Guid.NewGuid().ToString("n"));

    public FormMaintenanceTests() => Directory.CreateDirectory(_directory);

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

    private static async Task<(Guid FormId, string Token, Guid FieldId)> BuildFormAsync(HttpClient admin)
    {
        var created = await admin.PostAsJsonAsync("/api/v1/admin/forms", new { title = "Wartung" });
        var form = await created.Content.ReadFromJsonAsync<JsonElement>();
        var formId = form.GetProperty("id").GetGuid();

        var field = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "text", label = "Name", required = false, maxLength = 50,
        });
        var fieldId = (await field.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        return (formId, form.GetProperty("formToken").GetString()!, fieldId);
    }

    [Fact]
    public async Task The_public_form_route_answers_the_maintenance_notice()
    {
        using var factory = NewFactory();
        var admin = await factory.CreateSignedInClientAsync();
        var (_, token, _) = await BuildFormAsync(admin);

        await SetMaintenanceAsync(factory, true);

        var response = await factory.CreateClient().GetAsync($"/api/v1/f/{token}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task No_submission_is_accepted_while_maintenance_is_on()
    {
        using var factory = NewFactory();
        var admin = await factory.CreateSignedInClientAsync();
        var (formId, token, fieldId) = await BuildFormAsync(admin);

        await SetMaintenanceAsync(factory, true);

        var response = await factory.CreateClient().PostAsJsonAsync($"/api/v1/f/{token}/responses", new
        {
            values = new object[] { new { fieldId, value = "Anna" } },
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        await SetMaintenanceAsync(factory, false);
        var stored = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/forms/{formId}/responses");
        Assert.Empty(stored.EnumerateArray());
    }

    [Fact]
    public async Task The_forms_admin_routes_stay_reachable_during_maintenance()
    {
        // Unlike the public /f route, /admin/forms is not gated - it is session-authenticated
        // operator work, the same as every other admin CRUD surface (research R-8).
        using var factory = NewFactory();
        var admin = await factory.CreateSignedInClientAsync();
        await BuildFormAsync(admin);

        await SetMaintenanceAsync(factory, true);

        var listing = await admin.GetAsync("/api/v1/admin/forms");
        Assert.Equal(HttpStatusCode.OK, listing.StatusCode);

        var created = await admin.PostAsJsonAsync("/api/v1/admin/forms", new { title = "Während Wartung" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }
}
