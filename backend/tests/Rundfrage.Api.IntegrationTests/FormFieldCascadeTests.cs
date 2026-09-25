using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// The two delete-cascade directions that share only FormFieldValue and both must be exactly
/// right (010 FR-009, FR-042a; quickstart.md "easy to break" #3): deleting a field removes only
/// the values collected for it, across every response; deleting a response removes only the
/// values collected in it, across every field.
/// </summary>
public class FormFieldCascadeTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private static async Task<(Guid FormId, string Token, Guid NameFieldId, Guid EmailFieldId)>
        BuildFormAsync(HttpClient admin)
    {
        var created = await admin.PostAsJsonAsync("/api/v1/admin/forms", new { title = "Kaskade" });
        var form = await created.Content.ReadFromJsonAsync<JsonElement>();
        var formId = form.GetProperty("id").GetGuid();

        var name = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "text", label = "Name", required = false, maxLength = 100,
        });
        var email = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "email", label = "E-Mail", required = false,
        });

        return (
            formId,
            form.GetProperty("formToken").GetString()!,
            (await name.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(),
            (await email.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Removing_a_field_deletes_only_its_own_values_across_every_response()
    {
        // 010 FR-009.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var (formId, token, nameFieldId, emailFieldId) = await BuildFormAsync(admin);

        var anonymous = factory.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
            {
                values = new object[]
                {
                    new { fieldId = nameFieldId, value = $"Person {i}" },
                    new { fieldId = emailFieldId, value = $"person{i}@example.com" },
                },
            });
        }

        await admin.DeleteAsync($"/api/v1/admin/forms/{formId}/fields/{emailFieldId}");

        var responses = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/forms/{formId}/responses");
        var rows = responses.EnumerateArray().ToArray();

        Assert.Equal(3, rows.Length);
        foreach (var row in rows)
        {
            var values = row.GetProperty("values").EnumerateArray().ToArray();
            // The name value survives; the removed field's value is gone from every response.
            Assert.Single(values);
            Assert.Equal(nameFieldId, values[0].GetProperty("fieldId").GetGuid());
        }
    }

    [Fact]
    public async Task Deleting_a_response_removes_only_its_own_values_across_every_field()
    {
        // 010 FR-042a.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var (formId, token, nameFieldId, emailFieldId) = await BuildFormAsync(admin);

        var anonymous = factory.CreateClient();
        var responseIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var submitted = await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
            {
                values = new object[]
                {
                    new { fieldId = nameFieldId, value = $"Person {i}" },
                    new { fieldId = emailFieldId, value = $"person{i}@example.com" },
                },
            });
            var body = await submitted.Content.ReadFromJsonAsync<JsonElement>();
            responseIds.Add(body.GetProperty("id").GetGuid());
        }

        var deleteResponse = await admin.DeleteAsync(
            $"/api/v1/admin/forms/{formId}/responses/{responseIds[1]}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var responses = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/forms/{formId}/responses");
        var remainingIds = responses.EnumerateArray()
            .Select(r => r.GetProperty("id").GetGuid()).ToArray();

        Assert.Equal(2, remainingIds.Length);
        Assert.DoesNotContain(responseIds[1], remainingIds);
        Assert.Contains(responseIds[0], remainingIds);
        Assert.Contains(responseIds[2], remainingIds);

        // The form and its fields are entirely unaffected.
        var form = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/forms/{formId}");
        Assert.Equal(2, form.GetProperty("fields").EnumerateArray().Count());

        // Every surviving response still has both of its own values.
        foreach (var row in responses.EnumerateArray())
        {
            Assert.Equal(2, row.GetProperty("values").EnumerateArray().Count());
        }
    }
}
