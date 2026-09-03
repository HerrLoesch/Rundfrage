using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Security;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>FR-027a and FR-015a: the two ways a submission can be refused.</summary>
public class ResponseLimitTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static async Task<(string Token, Guid PollId, Guid[] DayIds)> CreatePollAsync(ApiFactory factory)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var created = await admin.PostAsJsonAsync(
            "/api/v1/admin/polls", new { title = "Grenztest", days = new[] { "2026-11-20" } });

        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        var token = summary.GetProperty("participantToken").GetString()!;
        var id = summary.GetProperty("id").GetGuid();

        var view = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/polls/{token}");
        var dayIds = view.GetProperty("days").EnumerateArray()
            .Select(d => d.GetProperty("id").GetGuid()).ToArray();

        return (token, id, dayIds);
    }

    [Fact]
    public async Task The_eleventh_submission_within_an_hour_is_refused_with_a_retry_hint()
    {
        // FR-027a, FR-027c, SC-020.
        using var factory = new ApiFactory(postgres.ConnectionString);
        var (token, _, dayIds) = await CreatePollAsync(factory);
        var anonymous = factory.CreateClient();

        HttpResponseMessage? refused = null;
        for (var i = 0; i < 11; i++)
        {
            var response = await anonymous.PostAsJsonAsync($"/api/v1/polls/{token}/responses", new
            {
                displayName = $"Person {i}",
                answers = new[] { new { dayId = dayIds[0], availability = "yes" } },
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
        // FR-027c: never accept and then discard.
        using var factory = new ApiFactory(postgres.ConnectionString);
        var (token, pollId, dayIds) = await CreatePollAsync(factory);
        var anonymous = factory.CreateClient();

        for (var i = 0; i < 15; i++)
        {
            await anonymous.PostAsJsonAsync($"/api/v1/polls/{token}/responses", new
            {
                displayName = $"Person {i}",
                answers = new[] { new { dayId = dayIds[0], availability = "yes" } },
            });
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
        var stored = await db.Responses.CountAsync(r => r.PollId == pollId);

        Assert.True(stored <= 10, $"{stored} responses were stored despite a limit of 10 per hour");
    }

    [Fact]
    public async Task Nothing_about_the_request_source_is_ever_stored()
    {
        // FR-027b and FR-042, SC-021: the limiter sees the source, the database never does.
        using var factory = new ApiFactory(postgres.ConnectionString);
        var (token, pollId, dayIds) = await CreatePollAsync(factory);

        await factory.CreateClient().PostAsJsonAsync($"/api/v1/polls/{token}/responses", new
        {
            displayName = "Emil",
            answers = new[] { new { dayId = dayIds[0], availability = "yes" } },
        });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT lower(column_name) FROM information_schema.columns WHERE table_schema = 'public'";

        var columns = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(0));
            }
        }

        Assert.DoesNotContain("ipaddress", columns);
        Assert.DoesNotContain("useragent", columns);
        Assert.DoesNotContain("remoteaddress", columns);
        Assert.True(await db.Responses.AnyAsync(r => r.PollId == pollId));
    }

    [Fact]
    public async Task The_thousand_and_first_response_is_refused_as_a_conflict()
    {
        // FR-015a. The rows are inserted directly - driving 1000 HTTP submissions would take
        // minutes and would hit the rate limit long before the cap.
        using var factory = new ApiFactory(postgres.ConnectionString);
        var (token, pollId, dayIds) = await CreatePollAsync(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
            db.Responses.AddRange(Enumerable.Range(0, Poll.MaxResponses).Select(i => new PollResponse
            {
                Id = Guid.CreateVersion7(),
                PollId = pollId,
                DisplayName = $"Person {i}",
                EditToken = CapabilityToken.Mint(),
                SubmittedAt = DateTimeOffset.UtcNow,
            }));
            await db.SaveChangesAsync();
        }

        var response = await factory.CreateClient().PostAsJsonAsync(
            $"/api/v1/polls/{token}/responses", new
            {
                displayName = "Der Eintausendunderste",
                answers = new[] { new { dayId = dayIds[0], availability = "yes" } },
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("poll_full", problem.GetProperty("code").GetString());
        Assert.Equal(Poll.MaxResponses, problem.GetProperty("limit").GetInt32());

        using var verify = factory.Services.CreateScope();
        var check = verify.ServiceProvider.GetRequiredService<RundfrageDbContext>();
        Assert.Equal(Poll.MaxResponses, await check.Responses.CountAsync(r => r.PollId == pollId));
    }
}
