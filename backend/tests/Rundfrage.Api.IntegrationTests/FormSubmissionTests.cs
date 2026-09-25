using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Rundfrage.Api.Http;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// User Story 2: a participant answering a form, blocked from submitting anything incomplete or
/// malformed (010 FR-016 to FR-030).
/// </summary>
public class FormSubmissionTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private static async Task<(Guid FormId, string Token, Guid NameFieldId, Guid EmailFieldId, Guid AgeFieldId)>
        BuildFormAsync(HttpClient admin)
    {
        var created = await admin.PostAsJsonAsync("/api/v1/admin/forms", new { title = "Anmeldung" });
        var form = await created.Content.ReadFromJsonAsync<JsonElement>();
        var formId = form.GetProperty("id").GetGuid();

        var name = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "text", label = "Name", required = true, maxLength = 100,
        });
        var email = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "email", label = "E-Mail", required = true,
        });
        var age = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "integer", label = "Alter", required = false,
        });

        var nameField = await name.Content.ReadFromJsonAsync<JsonElement>();
        var emailField = await email.Content.ReadFromJsonAsync<JsonElement>();
        var ageField = await age.Content.ReadFromJsonAsync<JsonElement>();

        return (
            formId,
            form.GetProperty("formToken").GetString()!,
            nameField.GetProperty("id").GetGuid(),
            emailField.GetProperty("id").GetGuid(),
            ageField.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task A_complete_valid_submission_is_accepted_and_stored()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var (formId, token, nameFieldId, emailFieldId, ageFieldId) = await BuildFormAsync(admin);

        var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
        {
            values = new object[]
            {
                new { fieldId = nameFieldId, value = "Anna" },
                new { fieldId = emailFieldId, value = "anna@example.com" },
                new { fieldId = ageFieldId, value = 30 },
            },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var stored = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/forms/{formId}/responses");
        Assert.Single(stored.EnumerateArray());
    }

    [Fact]
    public async Task A_missing_required_field_is_refused_naming_it_and_stores_nothing()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var (formId, token, nameFieldId, emailFieldId, _) = await BuildFormAsync(admin);

        var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
        {
            values = new object[] { new { fieldId = emailFieldId, value = "anna@example.com" } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ErrorCodes.SubmissionInvalid, body.GetProperty("code").GetString());

        var fieldErrors = body.GetProperty("fields").EnumerateArray().ToArray();
        Assert.Contains(fieldErrors, e =>
            e.GetProperty("fieldId").GetGuid() == nameFieldId
            && e.GetProperty("error").GetString() == ErrorCodes.FieldRequired);

        var stored = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/forms/{formId}/responses");
        Assert.Empty(stored.EnumerateArray());
    }

    [Fact]
    public async Task An_explicit_empty_string_for_a_required_field_is_refused_like_a_missing_one()
    {
        // 010 FR-017: a required Text field with no MinLength would otherwise accept "" as a
        // valid, non-empty answer - the server must catch this even though the frontend's own
        // isAnswered() never sends an empty string in the first place, since FR-017 explicitly
        // asks the server to validate "regardless of what the participant's browser already
        // checked" and this route is reachable by any client, not only the shipped UI.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var (formId, token, nameFieldId, emailFieldId, _) = await BuildFormAsync(admin);

        var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
        {
            values = new object[]
            {
                new { fieldId = nameFieldId, value = "" },
                new { fieldId = emailFieldId, value = "anna@example.com" },
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var fieldErrors = body.GetProperty("fields").EnumerateArray().ToArray();
        Assert.Contains(fieldErrors, e =>
            e.GetProperty("fieldId").GetGuid() == nameFieldId
            && e.GetProperty("error").GetString() == ErrorCodes.FieldRequired);

        var stored = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/forms/{formId}/responses");
        Assert.Empty(stored.EnumerateArray());
    }

    [Fact]
    public async Task A_malformed_value_is_refused_naming_the_field_and_stores_nothing()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var (formId, token, nameFieldId, emailFieldId, _) = await BuildFormAsync(admin);

        var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
        {
            values = new object[]
            {
                new { fieldId = nameFieldId, value = "Anna" },
                new { fieldId = emailFieldId, value = "not-an-email" },
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var fieldErrors = body.GetProperty("fields").EnumerateArray().ToArray();
        Assert.Contains(fieldErrors, e =>
            e.GetProperty("fieldId").GetGuid() == emailFieldId
            && e.GetProperty("error").GetString() == ErrorCodes.FieldInvalidFormat);

        var stored = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/forms/{formId}/responses");
        Assert.Empty(stored.EnumerateArray());
    }

    [Fact]
    public async Task An_optional_field_left_empty_does_not_block_submission()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var (_, token, nameFieldId, emailFieldId, _) = await BuildFormAsync(admin);

        var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
        {
            values = new object[]
            {
                new { fieldId = nameFieldId, value = "Anna" },
                new { fieldId = emailFieldId, value = "anna@example.com" },
            },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_required_boolean_field_left_unanswered_is_refused_as_missing_not_as_no()
    {
        // 010 FR-015: no default on the participant's behalf.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var created = await admin.PostAsJsonAsync("/api/v1/admin/forms", new { title = "RSVP" });
        var form = await created.Content.ReadFromJsonAsync<JsonElement>();
        var formId = form.GetProperty("id").GetGuid();
        var token = form.GetProperty("formToken").GetString();

        var boolField = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "boolean", label = "Kommst du?", required = true,
        });
        var boolFieldId = (await boolField.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new { values = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var fieldErrors = body.GetProperty("fields").EnumerateArray().ToArray();
        Assert.Contains(fieldErrors, e =>
            e.GetProperty("fieldId").GetGuid() == boolFieldId
            && e.GetProperty("error").GetString() == ErrorCodes.FieldRequired);
    }

    [Fact]
    public async Task Several_participants_can_each_submit_their_own_response()
    {
        // 010 FR-020: no cap, no identity check.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var (formId, token, nameFieldId, emailFieldId, _) = await BuildFormAsync(admin);

        var anonymous = factory.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            var response = await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
            {
                values = new object[]
                {
                    new { fieldId = nameFieldId, value = $"Person {i}" },
                    new { fieldId = emailFieldId, value = $"person{i}@example.com" },
                },
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var stored = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/forms/{formId}/responses");
        Assert.Equal(3, stored.EnumerateArray().Count());
    }
}
