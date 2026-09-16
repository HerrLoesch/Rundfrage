using System.Net.Http.Json;
using System.Text.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>Staging an Ersteller and their content through the real endpoints.</summary>
internal static class CreatorTestData
{
    /// <summary>Creates an Ersteller as the operator and returns its id and link token.</summary>
    public static async Task<(Guid Id, string Token)> NewCreatorAsync(
        ApiFactory factory, string name)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var response = await admin.PostAsJsonAsync("/api/v1/admin/creators", new { name });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return (body.GetProperty("id").GetGuid(), body.GetProperty("linkToken").GetString()!);
    }

    /// <summary>Creates a date poll through a creator link and returns its id.</summary>
    public static async Task<Guid> NewPollAsync(
        HttpClient client, string token, string title = "Grillabend")
    {
        var response = await client.PostAsJsonAsync($"/api/v1/e/{token}/polls", new
        {
            title,
            message = (string?)null,
            days = new[] { "2026-12-01", "2026-12-02" },
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    /// <summary>Creates a wish list through a creator link and returns its id.</summary>
    public static async Task<Guid> NewWishListAsync(
        HttpClient client, string token, string title = "Sommerfest")
    {
        var response = await client.PostAsJsonAsync($"/api/v1/e/{token}/wish-lists", new
        {
            title,
            description = (string?)null,
            targetDate = "2026-12-24",
            items = new[] { new { name = "Kuchen", wantedCount = 2 } },
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }
}
