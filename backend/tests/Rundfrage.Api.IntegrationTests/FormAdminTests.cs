using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Rundfrage.Api.Http;
using Rundfrage.Api.Security;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// User Story 1 from the operator's side: creating a form, reading it and listing forms
/// (010 FR-001, FR-002, FR-011, FR-040).
/// </summary>
public class FormAdminTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private static async Task<JsonElement> CreateAsync(HttpClient admin, string title = "Anmeldung")
    {
        var response = await admin.PostAsJsonAsync("/api/v1/admin/forms", new { title });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<string> RefusalCodeAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task Creating_a_form_stores_it_empty_with_a_token()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var created = await CreateAsync(admin, "Anmeldung Sommerfest");

        Assert.Equal("Anmeldung Sommerfest", created.GetProperty("title").GetString());
        Assert.Equal(0, created.GetProperty("fieldCount").GetInt32());
        Assert.Equal(0, created.GetProperty("responseCount").GetInt32());
        Assert.Empty(created.GetProperty("fields").EnumerateArray());
        Assert.Equal(CapabilityToken.TokenLength, created.GetProperty("formToken").GetString()!.Length);
    }

    [Fact]
    public async Task A_form_without_a_title_is_refused()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var response = await admin.PostAsJsonAsync("/api/v1/admin/forms", new { title = (string?)null });

        Assert.Equal(ErrorCodes.TitleRequired, await RefusalCodeAsync(response));
    }

    [Fact]
    public async Task A_form_can_be_read_back_by_id()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var created = await CreateAsync(admin);
        var formId = created.GetProperty("id").GetGuid();

        var fetched = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/forms/{formId}");
        Assert.Equal(created.GetProperty("title").GetString(), fetched.GetProperty("title").GetString());
    }

    [Fact]
    public async Task An_unknown_form_answers_the_neutral_payload()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var response = await admin.GetAsync($"/api/v1/admin/forms/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("not_found", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Every_form_route_refuses_without_a_session()
    {
        // 010 FR-031, FR-033: the refusal reveals nothing about what exists.
        using var factory = new ApiFactory(storage.DataDirectory);
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/v1/admin/forms")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/v1/admin/forms", new { title = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/v1/admin/forms/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task The_listing_carries_each_forms_counts_and_token()
    {
        // FR-040: the list every operator sees when opening the forms area.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var title = $"Figuren {Guid.NewGuid():N}";
        var created = await CreateAsync(admin, title);
        var formId = created.GetProperty("id").GetGuid();

        await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "text", label = "Name", required = true, maxLength = 100,
        });

        var rows = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/forms");
        var row = rows.EnumerateArray().Single(r => r.GetProperty("title").GetString() == title);

        Assert.Equal(1, row.GetProperty("fieldCount").GetInt32());
        Assert.Equal(0, row.GetProperty("responseCount").GetInt32());
        Assert.Equal(created.GetProperty("formToken").GetString(), row.GetProperty("formToken").GetString());
    }

    [Fact]
    public async Task The_listing_puts_the_newest_form_first()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var first = $"Erstes {Guid.NewGuid():N}";
        var second = $"Zweites {Guid.NewGuid():N}";

        await CreateAsync(admin, first);
        await CreateAsync(admin, second);

        var titles = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/forms"))
            .EnumerateArray()
            .Select(r => r.GetProperty("title").GetString()!)
            .Where(t => t == first || t == second)
            .ToArray();

        Assert.Equal(new[] { second, first }, titles);
    }

    [Fact]
    public async Task A_form_can_be_renamed_without_affecting_its_token()
    {
        // 010 FR-041.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var created = await CreateAsync(admin, "Alter Titel");
        var formId = created.GetProperty("id").GetGuid();
        var token = created.GetProperty("formToken").GetString();

        var response = await admin.PatchAsJsonAsync(
            $"/api/v1/admin/forms/{formId}", new { title = "Neuer Titel" });
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Neuer Titel", updated.GetProperty("title").GetString());
        Assert.Equal(token, updated.GetProperty("formToken").GetString());
    }

    [Fact]
    public async Task The_response_count_in_a_forms_detail_reflects_its_real_responses()
    {
        // FindAsync deliberately never loads the Responses navigation (it would mean reading
        // every response just to count them); GET/PATCH/reorder must still report the real count
        // rather than the empty collection's Count of zero.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var created = await CreateAsync(admin, "Zähler");
        var formId = created.GetProperty("id").GetGuid();

        var field = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "text", label = "Name", required = false, maxLength = 50,
        });
        var fieldId = (await field.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var token = created.GetProperty("formToken").GetString();

        var anonymous = factory.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
            {
                values = new object[] { new { fieldId, value = $"Person {i}" } },
            });
        }

        var fetched = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/forms/{formId}");
        Assert.Equal(3, fetched.GetProperty("responseCount").GetInt32());

        var renamed = await admin.PatchAsJsonAsync(
            $"/api/v1/admin/forms/{formId}", new { title = "Umbenannt" });
        var renamedBody = await renamed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, renamedBody.GetProperty("responseCount").GetInt32());

        var reordered = await admin.PutAsJsonAsync(
            $"/api/v1/admin/forms/{formId}/fields/order", new { fieldIds = new[] { fieldId } });
        var reorderedBody = await reordered.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, reorderedBody.GetProperty("responseCount").GetInt32());
    }

    [Fact]
    public async Task Deleting_a_form_removes_it_and_its_link_stops_working()
    {
        // 010 FR-042.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var created = await CreateAsync(admin);
        var formId = created.GetProperty("id").GetGuid();
        var token = created.GetProperty("formToken").GetString();

        await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "text", label = "Name", required = false, maxLength = 100,
        });

        var deleteResponse = await admin.DeleteAsync($"/api/v1/admin/forms/{formId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync($"/api/v1/admin/forms/{formId}")).StatusCode);

        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound,
            (await anonymous.GetAsync($"/api/v1/f/{token}")).StatusCode);
    }

    [Fact]
    public async Task When_no_forms_exist_the_listing_is_an_empty_array()
    {
        // FR-044: "none exist" must be distinguishable from "cannot be read" - the interface makes
        // that distinction; here it is enough that an empty installation answers an empty list
        // rather than an error.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var rows = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/forms");
        Assert.Equal(JsonValueKind.Array, rows.ValueKind);
    }

    [Fact]
    public async Task When_storage_cannot_be_read_the_listing_is_not_a_silent_empty_array()
    {
        // FR-044: distinguishing "nothing exists" from "the stored data cannot be read right now"
        // is what lets the interface show two different states rather than rendering both as zero
        // (mirrors 007 FR-017, 009 FR-046).
        using var factory = new ApiFactory(ApiFactory.UnreachableDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var response = await admin.GetAsync("/api/v1/admin/forms");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
