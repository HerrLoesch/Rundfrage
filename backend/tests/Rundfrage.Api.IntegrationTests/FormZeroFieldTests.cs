using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 010 FR-021: an unknown, malformed, zero-field or deleted form link must all produce the one
/// indistinguishable response.
/// </summary>
public class FormZeroFieldTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private static async Task<HttpResponseMessage> NotFoundResponseFor(
        HttpClient anonymous, string token) =>
        await anonymous.GetAsync($"/api/v1/f/{token}");

    private static async Task AssertNeutralAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("not_found", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_form_with_zero_fields_is_refused_like_an_unknown_token()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var created = await admin.PostAsJsonAsync("/api/v1/admin/forms", new { title = "Leer" });
        var form = await created.Content.ReadFromJsonAsync<JsonElement>();
        var token = form.GetProperty("formToken").GetString()!;

        var anonymous = factory.CreateClient();
        await AssertNeutralAsync(await NotFoundResponseFor(anonymous, token));
    }

    [Fact]
    public async Task A_malformed_token_is_refused_the_same_way()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var anonymous = factory.CreateClient();

        await AssertNeutralAsync(await NotFoundResponseFor(anonymous, "not-a-real-token"));
    }

    [Fact]
    public async Task An_unknown_but_well_formed_token_is_refused_the_same_way()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var anonymous = factory.CreateClient();

        await AssertNeutralAsync(
            await NotFoundResponseFor(anonymous, Rundfrage.Api.Security.CapabilityToken.Mint()));
    }

    [Fact]
    public async Task A_deleted_forms_former_token_is_refused_the_same_way()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var created = await admin.PostAsJsonAsync("/api/v1/admin/forms", new { title = "Weg" });
        var form = await created.Content.ReadFromJsonAsync<JsonElement>();
        var formId = form.GetProperty("id").GetGuid();
        var token = form.GetProperty("formToken").GetString()!;

        await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "text", label = "Name", required = false, maxLength = 50,
        });
        await admin.DeleteAsync($"/api/v1/admin/forms/{formId}");

        var anonymous = factory.CreateClient();
        await AssertNeutralAsync(await NotFoundResponseFor(anonymous, token));
    }

    [Fact]
    public async Task A_form_with_a_field_is_reachable()
    {
        // The positive control: without this, the four refusal tests above could all be passing
        // for the wrong reason (every request 404ing regardless of the form's state).
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var created = await admin.PostAsJsonAsync("/api/v1/admin/forms", new { title = "Erreichbar" });
        var form = await created.Content.ReadFromJsonAsync<JsonElement>();
        var formId = form.GetProperty("id").GetGuid();
        var token = form.GetProperty("formToken").GetString()!;

        await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "text", label = "Name", required = false, maxLength = 50,
        });

        var anonymous = factory.CreateClient();
        var response = await NotFoundResponseFor(anonymous, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
