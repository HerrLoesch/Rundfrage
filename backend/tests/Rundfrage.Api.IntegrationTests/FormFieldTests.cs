using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Http;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// Adding, editing, removing and reordering fields on a form (010 FR-003 to FR-011a).
/// </summary>
public class FormFieldTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private static async Task<Guid> CreateFormAsync(HttpClient admin, string title = "Formular")
    {
        var response = await admin.PostAsJsonAsync("/api/v1/admin/forms", new { title });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> AddFieldAsync(
        HttpClient admin, Guid formId, string type = "text", string label = "Name",
        bool required = false, int? maxLength = 100, int? minLength = null)
    {
        var response = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type, label, required, maxLength, minLength,
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task A_field_can_be_added_with_its_type_and_label()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var formId = await CreateFormAsync(admin);

        var field = await AddFieldAsync(admin, formId, type: "email", label: "E-Mail", required: true,
            maxLength: null);

        Assert.Equal("email", field.GetProperty("type").GetString());
        Assert.Equal("E-Mail", field.GetProperty("label").GetString());
        Assert.True(field.GetProperty("required").GetBoolean());
        Assert.Equal(0, field.GetProperty("displayOrder").GetInt32());
    }

    [Fact]
    public async Task Fields_are_appended_in_the_order_they_are_added()
    {
        // 010 FR-010.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var formId = await CreateFormAsync(admin);

        await AddFieldAsync(admin, formId, label: "Erstes");
        await AddFieldAsync(admin, formId, label: "Zweites");
        await AddFieldAsync(admin, formId, label: "Drittes");

        var form = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/forms/{formId}");
        var labels = form.GetProperty("fields").EnumerateArray()
            .Select(f => f.GetProperty("label").GetString()).ToArray();

        Assert.Equal(new[] { "Erstes", "Zweites", "Drittes" }, labels);
    }

    [Fact]
    public async Task A_text_field_without_a_maximum_length_is_refused()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var formId = await CreateFormAsync(admin);

        var response = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "text", label = "Name", required = false, maxLength = (int?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ErrorCodes.TextMaxLengthMissing, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_field_can_be_edited_after_the_form_has_collected_responses()
    {
        // 010 FR-008: editing is permitted at any time, including after real answers exist.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var formId = await CreateFormAsync(admin);
        var field = await AddFieldAsync(admin, formId, label: "Name", required: false);
        var fieldId = field.GetProperty("id").GetGuid();

        var formDetail = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/forms/{formId}");
        var token = formDetail.GetProperty("formToken").GetString();

        var anonymous = factory.CreateClient();
        var submit = await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
        {
            values = new[] { new { fieldId, value = "Anna" } },
        });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);

        var editResponse = await admin.PatchAsJsonAsync(
            $"/api/v1/admin/forms/{formId}/fields/{fieldId}",
            new { label = "Vollständiger Name", required = true });
        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);

        var edited = await editResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Vollständiger Name", edited.GetProperty("label").GetString());
        Assert.True(edited.GetProperty("required").GetBoolean());

        // The existing response's stored value is untouched by the edit.
        var responses = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/forms/{formId}/responses");
        var storedValue = responses.EnumerateArray().Single()
            .GetProperty("values").EnumerateArray().Single()
            .GetProperty("value").GetString();
        Assert.Equal("Anna", storedValue);
    }

    [Fact]
    public async Task A_type_included_in_an_edit_request_is_silently_ignored()
    {
        // 010 FR-008, spec clarification 2026-09-22: there is no code path that changes a
        // field's type; a "type" property in the PATCH body has no effect at all.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var formId = await CreateFormAsync(admin);
        var field = await AddFieldAsync(admin, formId, type: "text", label: "Name");
        var fieldId = field.GetProperty("id").GetGuid();

        var response = await admin.PatchAsJsonAsync(
            $"/api/v1/admin/forms/{formId}/fields/{fieldId}",
            new { type = "integer", label = "Name" });
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("text", updated.GetProperty("type").GetString());
    }

    [Fact]
    public async Task An_explicit_null_clears_a_text_fields_minimum_length()
    {
        // A patch that omits minLength must leave it untouched, and a patch that explicitly sets
        // it to null must clear it - the two must not collapse into the same "leave it" behaviour,
        // or a minimum could be set once but never removed again.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var formId = await CreateFormAsync(admin);
        var field = await AddFieldAsync(admin, formId, type: "text", label: "Name", minLength: 2);
        var fieldId = field.GetProperty("id").GetGuid();

        Assert.Equal(2, field.GetProperty("minLength").GetInt32());

        // Omitting minLength entirely must leave it as it is.
        var untouched = await admin.PatchAsJsonAsync(
            $"/api/v1/admin/forms/{formId}/fields/{fieldId}", new { label = "Name" });
        var untouchedBody = await untouched.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, untouchedBody.GetProperty("minLength").GetInt32());

        // An explicit null must clear it.
        var cleared = await admin.PatchAsJsonAsync(
            $"/api/v1/admin/forms/{formId}/fields/{fieldId}", new { minLength = (int?)null });
        cleared.EnsureSuccessStatusCode();
        var clearedBody = await cleared.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, clearedBody.GetProperty("minLength").ValueKind);

        // And the clear must persist - not just appear in the immediate response.
        var reread = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/forms/{formId}");
        var rereadField = reread.GetProperty("fields").EnumerateArray().Single();
        Assert.Equal(JsonValueKind.Null, rereadField.GetProperty("minLength").ValueKind);
    }

    [Fact]
    public async Task Removing_a_field_renumbers_the_remainder_and_deletes_its_values()
    {
        // 010 FR-009: removal cascades to values; DisplayOrder stays dense (research R-4).
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var formId = await CreateFormAsync(admin);
        var first = await AddFieldAsync(admin, formId, label: "Erstes");
        var second = await AddFieldAsync(admin, formId, label: "Zweites");
        var third = await AddFieldAsync(admin, formId, label: "Drittes");

        var deleteResponse = await admin.DeleteAsync(
            $"/api/v1/admin/forms/{formId}/fields/{second.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var form = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/forms/{formId}");
        var fields = form.GetProperty("fields").EnumerateArray().ToArray();

        Assert.Equal(2, fields.Length);
        Assert.Equal("Erstes", fields[0].GetProperty("label").GetString());
        Assert.Equal(0, fields[0].GetProperty("displayOrder").GetInt32());
        Assert.Equal("Drittes", fields[1].GetProperty("label").GetString());
        Assert.Equal(1, fields[1].GetProperty("displayOrder").GetInt32());
    }

    [Fact]
    public async Task Fields_can_be_reordered_by_naming_the_new_sequence()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var formId = await CreateFormAsync(admin);
        var first = await AddFieldAsync(admin, formId, label: "Erstes");
        var second = await AddFieldAsync(admin, formId, label: "Zweites");

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/forms/{formId}/fields/order", new
        {
            fieldIds = new[] { second.GetProperty("id").GetGuid(), first.GetProperty("id").GetGuid() },
        });
        response.EnsureSuccessStatusCode();

        var form = await response.Content.ReadFromJsonAsync<JsonElement>();
        var labels = form.GetProperty("fields").EnumerateArray()
            .Select(f => f.GetProperty("label").GetString()).ToArray();

        Assert.Equal(new[] { "Zweites", "Erstes" }, labels);
    }

    [Fact]
    public async Task A_reorder_naming_the_wrong_set_of_fields_is_refused()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var formId = await CreateFormAsync(admin);
        await AddFieldAsync(admin, formId, label: "Erstes");

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/forms/{formId}/fields/order", new
        {
            fieldIds = new[] { Guid.NewGuid() },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ErrorCodes.FieldSetMismatch, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_form_may_not_hold_more_than_fifty_fields()
    {
        // 010 FR-011a.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var formId = await CreateFormAsync(admin);

        for (var i = 0; i < Form.MaxFields; i++)
        {
            var ok = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
            {
                type = "text", label = $"Feld {i}", required = false, maxLength = 50,
            });
            ok.EnsureSuccessStatusCode();
        }

        var refused = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "text", label = "Einundfünfzigstes", required = false, maxLength = 50,
        });

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var body = await refused.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ErrorCodes.FieldLimitReached, body.GetProperty("code").GetString());
        Assert.Equal(Form.MaxFields, body.GetProperty("limit").GetInt32());
    }

    [Fact]
    public async Task Adding_fields_concurrently_never_exceeds_the_cap()
    {
        // The same concurrency shape CreatorLimitTests exercises for the 100-Ersteller cap.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var formId = await CreateFormAsync(admin);

        for (var i = 0; i < Form.MaxFields - 5; i++)
        {
            await AddFieldAsync(admin, formId, label: $"Feld {i}");
        }

        var attempts = Enumerable.Range(0, 20).Select(i => admin.PostAsJsonAsync(
            $"/api/v1/admin/forms/{formId}/fields",
            new { type = "text", label = $"Gleichzeitig {i}", required = false, maxLength = 50 }));

        var results = await Task.WhenAll(attempts);
        var succeeded = results.Count(r => r.StatusCode == HttpStatusCode.Created);

        Assert.Equal(5, succeeded);

        var form = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/forms/{formId}");
        Assert.Equal(Form.MaxFields, form.GetProperty("fieldCount").GetInt32());
    }

    [Fact]
    public async Task Removing_two_different_fields_concurrently_leaves_a_dense_consistent_order()
    {
        // RemoveFieldAsync's delete-then-renumber runs inside the same immediate write
        // transaction AddFieldAsync's cap check uses, for exactly this reason: two concurrent
        // removals on the same form must not interleave their independent read-then-rewrite of
        // every remaining field's DisplayOrder.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var formId = await CreateFormAsync(admin);

        var fields = new List<Guid>();
        for (var i = 0; i < 10; i++)
        {
            var field = await AddFieldAsync(admin, formId, label: $"Feld {i}");
            fields.Add(field.GetProperty("id").GetGuid());
        }

        await Task.WhenAll(
            admin.DeleteAsync($"/api/v1/admin/forms/{formId}/fields/{fields[3]}"),
            admin.DeleteAsync($"/api/v1/admin/forms/{formId}/fields/{fields[7]}"));

        var form = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/forms/{formId}");
        var remaining = form.GetProperty("fields").EnumerateArray().ToArray();

        Assert.Equal(8, remaining.Length);
        var orders = remaining.Select(f => f.GetProperty("displayOrder").GetInt32()).OrderBy(o => o).ToArray();
        Assert.Equal(Enumerable.Range(0, 8), orders);
    }
}
