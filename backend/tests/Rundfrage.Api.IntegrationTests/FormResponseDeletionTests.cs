using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>Deleting a single response, distinct from deleting a whole form (010 FR-042a).</summary>
public class FormResponseDeletionTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private static async Task<(Guid FormId, string Token, Guid FieldId)> BuildFormAsync(HttpClient admin)
    {
        var created = await admin.PostAsJsonAsync("/api/v1/admin/forms", new { title = "Löschen" });
        var form = await created.Content.ReadFromJsonAsync<JsonElement>();
        var formId = form.GetProperty("id").GetGuid();

        var field = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "text", label = "Name", required = false, maxLength = 50,
        });

        return (
            formId,
            form.GetProperty("formToken").GetString()!,
            (await field.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Deleting_one_response_leaves_the_forms_link_and_other_responses_untouched()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var (formId, token, fieldId) = await BuildFormAsync(admin);

        var anonymous = factory.CreateClient();
        var first = await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
        {
            values = new object[] { new { fieldId, value = "Erste" } },
        });
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
        {
            values = new object[] { new { fieldId, value = "Zweite" } },
        });

        await admin.DeleteAsync($"/api/v1/admin/forms/{formId}/responses/{firstId}");

        // The link still works - the form itself is untouched.
        var stillWorks = await anonymous.GetAsync($"/api/v1/f/{token}");
        Assert.Equal(HttpStatusCode.OK, stillWorks.StatusCode);

        var responses = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/forms/{formId}/responses");
        Assert.Single(responses.EnumerateArray());
    }

    [Fact]
    public async Task Deleting_an_unknown_response_answers_the_neutral_payload()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var (formId, _, _) = await BuildFormAsync(admin);

        var response = await admin.DeleteAsync(
            $"/api/v1/admin/forms/{formId}/responses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("not_found", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_response_cannot_be_deleted_through_a_different_forms_id()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var (formId, token, fieldId) = await BuildFormAsync(admin);
        var (otherFormId, _, _) = await BuildFormAsync(admin);

        var anonymous = factory.CreateClient();
        var submitted = await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
        {
            values = new object[] { new { fieldId, value = "Anna" } },
        });
        var responseId = (await submitted.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var response = await admin.DeleteAsync(
            $"/api/v1/admin/forms/{otherFormId}/responses/{responseId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var stillThere = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/forms/{formId}/responses");
        Assert.Single(stillThere.EnumerateArray());
    }

    [Fact]
    public async Task The_response_delete_route_requires_a_session()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var anonymous = factory.CreateClient();

        var response = await anonymous.DeleteAsync(
            $"/api/v1/admin/forms/{Guid.NewGuid()}/responses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
