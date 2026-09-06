using System.Net.Http.Json;
using System.Text.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>Shared plumbing for the import tests, so each file states only its own case.</summary>
public static class ImportTestHelper
{
    public const string ImportRoute = "/api/v1/admin/polls/import";

    public static async Task<(HttpResponseMessage Response, JsonElement Body)> ImportAsync(
        ApiFactory factory, MultipartFormDataContent content)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var response = await admin.PostAsync(ImportRoute, content);
        var raw = await response.Content.ReadAsStringAsync();

        var body = string.IsNullOrWhiteSpace(raw)
            ? default
            : JsonDocument.Parse(raw).RootElement;

        return (response, body);
    }

    public static Task<(HttpResponseMessage Response, JsonElement Body)> ImportAsync(
        ApiFactory factory, ExportDocumentBuilder document) =>
        ImportAsync(factory, document.ToFormContent());

    /// <summary>Creates a poll through the ordinary admin route and exports it, as an operator would.</summary>
    public static async Task<(Guid PollId, string Token, string ExportJson)> CreateAndExportAsync(
        ApiFactory factory, string title, string? message, string[] days)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var created = await admin.PostAsJsonAsync(
            "/api/v1/admin/polls", new { title, message, days });
        created.EnsureSuccessStatusCode();

        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = summary.GetProperty("id").GetGuid();
        var token = summary.GetProperty("participantToken").GetString()!;

        var export = await admin.GetAsync($"/api/v1/admin/polls/{id}/export");
        export.EnsureSuccessStatusCode();

        return (id, token, await export.Content.ReadAsStringAsync());
    }

    public static async Task AnswerAsync(
        ApiFactory factory, string token, string name, params (Guid Day, string Availability)[] answers)
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            $"/api/v1/polls/{token}/responses",
            new
            {
                displayName = name,
                answers = answers.Select(a => new { dayId = a.Day, availability = a.Availability }),
            });

        response.EnsureSuccessStatusCode();
    }

    public static async Task<Guid[]> DayIdsAsync(ApiFactory factory, string token)
    {
        var view = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{token}");
        return [.. view.GetProperty("days").EnumerateArray().Select(d => d.GetProperty("id").GetGuid())];
    }

    public static string[] SkipReasons(JsonElement body) =>
        [.. body.GetProperty("skipped").EnumerateArray().Select(s => s.GetProperty("reason").GetString()!)];

    public static string Code(JsonElement body) => body.GetProperty("code").GetString()!;
}
