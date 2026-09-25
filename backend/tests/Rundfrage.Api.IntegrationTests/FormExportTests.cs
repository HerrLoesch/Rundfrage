using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// User Story 3: taking a form's responses out as CSV or JSON (010 FR-034 to FR-039).
/// </summary>
public class FormExportTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private static async Task<(Guid FormId, string Token, Guid NameFieldId, Guid AgeFieldId, Guid RsvpFieldId)>
        BuildFormAsync(HttpClient admin)
    {
        var created = await admin.PostAsJsonAsync("/api/v1/admin/forms", new { title = "Export" });
        var form = await created.Content.ReadFromJsonAsync<JsonElement>();
        var formId = form.GetProperty("id").GetGuid();

        var name = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "text", label = "Name", required = true, maxLength = 100,
        });
        var age = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "integer", label = "Alter", required = false,
        });
        var rsvp = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "boolean", label = "Kommt", required = false,
        });

        return (
            formId,
            form.GetProperty("formToken").GetString()!,
            (await name.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(),
            (await age.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(),
            (await rsvp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Csv_export_has_one_header_per_field_and_one_row_per_response_in_field_order()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var (formId, token, nameFieldId, ageFieldId, rsvpFieldId) = await BuildFormAsync(admin);

        var anonymous = factory.CreateClient();
        await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
        {
            values = new object[]
            {
                new { fieldId = nameFieldId, value = "Anna" },
                new { fieldId = ageFieldId, value = 30 },
                new { fieldId = rsvpFieldId, value = true },
            },
        });

        var response = await admin.GetAsync($"/api/v1/admin/forms/{formId}/export/csv");
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

        var csv = await response.Content.ReadAsStringAsync();
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Name,Alter,Kommt", lines[0]);
        Assert.Equal("Anna,30,TRUE", lines[1]);
    }

    [Fact]
    public async Task Json_export_has_one_object_per_response_with_typed_values()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var (formId, token, nameFieldId, ageFieldId, rsvpFieldId) = await BuildFormAsync(admin);

        var anonymous = factory.CreateClient();
        await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
        {
            values = new object[]
            {
                new { fieldId = nameFieldId, value = "Anna" },
                new { fieldId = ageFieldId, value = 30 },
                new { fieldId = rsvpFieldId, value = true },
            },
        });

        var document = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/forms/{formId}/export/json");

        var response = document.GetProperty("responses").EnumerateArray().Single();
        var values = response.GetProperty("values");

        Assert.Equal("Anna", values.GetProperty("Name").GetString());
        Assert.Equal(JsonValueKind.Number, values.GetProperty("Alter").ValueKind);
        Assert.Equal(30, values.GetProperty("Alter").GetInt32());
        Assert.Equal(JsonValueKind.True, values.GetProperty("Kommt").ValueKind);
    }

    [Fact]
    public async Task Exporting_a_form_with_zero_responses_produces_a_valid_empty_file()
    {
        // 010 FR-039.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var (formId, _, _, _, _) = await BuildFormAsync(admin);

        var csvResponse = await admin.GetAsync($"/api/v1/admin/forms/{formId}/export/csv");
        csvResponse.EnsureSuccessStatusCode();
        var csv = await csvResponse.Content.ReadAsStringAsync();
        Assert.Equal("Name,Alter,Kommt\r\n", csv);

        var json = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/forms/{formId}/export/json");
        Assert.Empty(json.GetProperty("responses").EnumerateArray());
    }

    [Fact]
    public async Task Export_reflects_the_current_field_order_regardless_of_when_responses_were_collected()
    {
        // 010 FR-010, SC-005.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var (formId, token, nameFieldId, ageFieldId, rsvpFieldId) = await BuildFormAsync(admin);

        var anonymous = factory.CreateClient();
        await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
        {
            values = new object[] { new { fieldId = nameFieldId, value = "Vorher" } },
        });

        // Reorder: Alter first, then Name, then Kommt.
        await admin.PutAsJsonAsync($"/api/v1/admin/forms/{formId}/fields/order", new
        {
            fieldIds = new[] { ageFieldId, nameFieldId, rsvpFieldId },
        });

        await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
        {
            values = new object[] { new { fieldId = nameFieldId, value = "Nachher" } },
        });

        var csvResponse = await admin.GetAsync($"/api/v1/admin/forms/{formId}/export/csv");
        var csv = await csvResponse.Content.ReadAsStringAsync();
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Alter,Name,Kommt", lines[0]);
        // Both the response collected before and after the reorder are shown in the new order.
        Assert.Equal(",Vorher,", lines[1]);
        Assert.Equal(",Nachher,", lines[2]);
    }

    [Fact]
    public async Task A_field_added_after_a_response_exports_that_responses_value_as_absent()
    {
        // 010 FR-038.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var created = await admin.PostAsJsonAsync("/api/v1/admin/forms", new { title = "Später" });
        var form = await created.Content.ReadFromJsonAsync<JsonElement>();
        var formId = form.GetProperty("id").GetGuid();
        var token = form.GetProperty("formToken").GetString();

        var name = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "text", label = "Name", required = true, maxLength = 100,
        });
        var nameFieldId = (await name.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var anonymous = factory.CreateClient();
        await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
        {
            values = new object[] { new { fieldId = nameFieldId, value = "Anna" } },
        });

        // A field added after the response exists.
        await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "text", label = "Kommentar", required = false, maxLength = 200,
        });

        var jsonDoc = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/forms/{formId}/export/json");
        var values = jsonDoc.GetProperty("responses").EnumerateArray().Single().GetProperty("values");
        Assert.False(values.TryGetProperty("Kommentar", out _));

        var csvResponse = await admin.GetAsync($"/api/v1/admin/forms/{formId}/export/csv");
        var csv = await csvResponse.Content.ReadAsStringAsync();
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Name,Kommentar", lines[0]);
        Assert.Equal("Anna,", lines[1]);
    }

    [Fact]
    public async Task Duplicate_labels_are_disambiguated_in_both_export_formats()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var created = await admin.PostAsJsonAsync("/api/v1/admin/forms", new { title = "Doppelt" });
        var form = await created.Content.ReadFromJsonAsync<JsonElement>();
        var formId = form.GetProperty("id").GetGuid();
        var token = form.GetProperty("formToken").GetString();

        var first = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "text", label = "Name", required = false, maxLength = 100,
        });
        var second = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "text", label = "Name", required = false, maxLength = 100,
        });

        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var anonymous = factory.CreateClient();
        await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
        {
            values = new object[]
            {
                new { fieldId = firstId, value = "Erster" },
                new { fieldId = secondId, value = "Zweiter" },
            },
        });

        var csvResponse = await admin.GetAsync($"/api/v1/admin/forms/{formId}/export/csv");
        var csv = await csvResponse.Content.ReadAsStringAsync();
        Assert.StartsWith("Name,Name (2)\r\n", csv);

        var jsonDoc = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/forms/{formId}/export/json");
        var values = jsonDoc.GetProperty("responses").EnumerateArray().Single().GetProperty("values");
        Assert.Equal("Erster", values.GetProperty("Name").GetString());
        Assert.Equal("Zweiter", values.GetProperty("Name (2)").GetString());
    }

    [Fact]
    public async Task Export_routes_require_a_session()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/v1/admin/forms/{Guid.NewGuid()}/export/csv")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/v1/admin/forms/{Guid.NewGuid()}/export/json")).StatusCode);
    }
}
