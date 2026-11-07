using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 010 FR-022, research R-9: form submissions share the existing 10/hour participant policy - no
/// new one is introduced.
/// </summary>
public class FormRateLimitTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private static async Task<(string Token, Guid FieldId)> BuildFormAsync(HttpClient admin)
    {
        var created = await admin.PostAsJsonAsync("/api/v1/admin/forms", new { title = "Grenztest" });
        var form = await created.Content.ReadFromJsonAsync<JsonElement>();
        var formId = form.GetProperty("id").GetGuid();

        var field = await admin.PostAsJsonAsync($"/api/v1/admin/forms/{formId}/fields", new
        {
            type = "text", label = "Name", required = false, maxLength = 50,
        });

        return (
            form.GetProperty("formToken").GetString()!,
            (await field.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task The_eleventh_submission_within_an_hour_is_refused_with_a_retry_hint()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var (token, fieldId) = await BuildFormAsync(admin);
        var anonymous = factory.CreateClient();

        HttpResponseMessage? refused = null;
        for (var i = 0; i < 11; i++)
        {
            var response = await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
            {
                values = new object[] { new { fieldId, value = $"Person {i}" } },
            });

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                refused = response;
                break;
            }
        }

        Assert.NotNull(refused);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("too_many_requests", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("retryAfterSeconds").GetInt32() > 0);
    }

    [Fact]
    public async Task A_refused_submission_stores_nothing()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var (token, fieldId) = await BuildFormAsync(admin);
        var anonymous = factory.CreateClient();

        for (var i = 0; i < 15; i++)
        {
            await anonymous.PostAsJsonAsync($"/api/v1/f/{token}/responses", new
            {
                values = new object[] { new { fieldId, value = $"Person {i}" } },
            });
        }

        var form = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/forms")!;
        var formId = form.EnumerateArray()
            .Single(f => f.GetProperty("formToken").GetString() == token)
            .GetProperty("id").GetGuid();

        var stored = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/forms/{formId}/responses");
        Assert.True(
            stored.EnumerateArray().Count() <= 10,
            "more than 10 responses were stored despite the shared 10/hour policy");
    }

    [Fact]
    public async Task Reading_the_form_definition_is_never_rate_limited()
    {
        // 010 research R-10: a required field for the participant's first load must never itself
        // be refused.
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();
        var (token, _) = await BuildFormAsync(admin);
        var anonymous = factory.CreateClient();

        for (var i = 0; i < 20; i++)
        {
            var response = await anonymous.GetAsync($"/api/v1/f/{token}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
