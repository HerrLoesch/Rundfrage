using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Security;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// FR-036c and SC-016: the grid has to survive its own maximum - 1000 responses across 100
/// candidate days is 100,000 cells. This is the sizing case, not an error case.
/// </summary>
public class ScaleTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task A_poll_at_the_declared_limits_returns_its_first_page_within_five_seconds()
    {
        using var factory = new ApiFactory(postgres.ConnectionString);
        var admin = await factory.CreateSignedInClientAsync();

        var days = Enumerable.Range(0, Poll.MaxCandidateDays)
            .Select(i => new DateOnly(2027, 1, 1).AddDays(i).ToString("yyyy-MM-dd"))
            .ToArray();

        var created = await admin.PostAsJsonAsync(
            "/api/v1/admin/polls", new { title = "Maximalfall", days });
        created.EnsureSuccessStatusCode();

        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        var pollId = summary.GetProperty("id").GetGuid();
        var token = summary.GetProperty("participantToken").GetString()!;

        // Seeded directly: 1000 HTTP submissions would take minutes and prove nothing extra.
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
                    SubmittedAt = DateTimeOffset.UtcNow,
                    // Half the days answered, which is the realistic shape and exercises the
                    // absence-is-a-state design at scale.
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
        var view = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{token}");
        stopwatch.Stop();

        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"the first page took {stopwatch.Elapsed.TotalSeconds:F1}s; SC-016 allows 5");

        // Paged, not the whole grid: 100,000 cells never cross the wire at once.
        Assert.Equal(1000, view.GetProperty("responseCount").GetInt32());
        Assert.Equal(50, view.GetProperty("responses").GetArrayLength());
        Assert.Equal(20, view.GetProperty("pageCount").GetInt32());
        Assert.Equal(100, view.GetProperty("days").GetArrayLength());
        Assert.Equal(100, view.GetProperty("totals").GetArrayLength());
    }
}
