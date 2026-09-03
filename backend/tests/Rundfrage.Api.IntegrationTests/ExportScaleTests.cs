using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Security;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// SC-011: a poll at the limits of feature 002 - 1000 responses across 100 days - exports as one
/// download within ten seconds.
/// </summary>
/// <remarks>
/// The grid is paged because 100,000 cells must not cross the wire at once (002 FR-036c). An
/// export cannot be paged: a file split into twenty parts is not "the data in hand", which is
/// what the whole file-based idea was for. So the size the grid is allowed to avoid is the size
/// this has to carry, and that is worth measuring rather than assuming.
/// </remarks>
public class ExportScaleTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    [Fact]
    public async Task A_poll_at_the_declared_limits_exports_as_one_download_within_ten_seconds()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        var days = Enumerable.Range(0, Poll.MaxCandidateDays)
            .Select(i => new DateOnly(2027, 1, 1).AddDays(i).ToString("yyyy-MM-dd"))
            .ToArray();

        var created = await admin.PostAsJsonAsync(
            "/api/v1/admin/polls", new { title = "Maximalfall", days });
        created.EnsureSuccessStatusCode();

        var pollId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
            var dayIds = db.CandidateDays.Where(d => d.PollId == pollId).Select(d => d.Id).ToList();

            for (var i = 0; i < Poll.MaxResponses; i++)
            {
                db.Responses.Add(new PollResponse
                {
                    Id = Guid.CreateVersion7(),
                    PollId = pollId,
                    DisplayName = $"Person {i}",
                    EditToken = CapabilityToken.Mint(),
                    SubmittedAt = DateTime.UtcNow,
                    Answers = [.. dayIds.Take(50).Select(d => new DayAnswer
                    {
                        CandidateDayId = d,
                        Availability = Availability.Yes,
                    })],
                });
            }

            await db.SaveChangesAsync();
        }

        var stopwatch = Stopwatch.StartNew();
        var response = await admin.GetAsync($"/api/v1/admin/polls/{pollId}/export");
        var raw = await response.Content.ReadAsStringAsync();
        stopwatch.Stop();

        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"the export took {stopwatch.Elapsed.TotalSeconds:F1}s; SC-011 allows 10");

        // Parsed rather than merely counted: FR-016 asks for JSON that needs no repair, and a
        // truncated download would still have a plausible length.
        var document = JsonDocument.Parse(raw).RootElement;
        Assert.Equal(1000, document.GetProperty("responses").GetArrayLength());
        Assert.Equal(100, document.GetProperty("poll").GetProperty("days").GetArrayLength());
        Assert.Equal(50, document.GetProperty("responses")[0].GetProperty("answers").GetArrayLength());
    }
}
