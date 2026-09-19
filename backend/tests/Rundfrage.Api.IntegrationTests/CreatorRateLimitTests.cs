using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 009 FR-036, FR-036a, FR-036c: what bounds an Ersteller's activity, and what does not.
/// </summary>
public sealed class CreatorRateLimitTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "rundfrage-tests", Guid.NewGuid().ToString("n"));

    public CreatorRateLimitTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static Task<HttpResponseMessage> CreateWishListAsync(
        HttpClient client, string token, string title) =>
        client.PostAsJsonAsync($"/api/v1/e/{token}/wish-lists", new
        {
            title,
            targetDate = "2026-12-24",
            items = new[] { new { name = "Kuchen", wantedCount = 1 } },
        });

    [Fact]
    public async Task Writes_are_accepted_up_to_the_budget_and_the_next_one_is_refused_with_the_wait()
    {
        // Three rather than sixty, so the boundary is reachable without making sixty requests.
        // The number under test is the configurability, not the constant: the constant is pinned
        // separately below.
        using var factory = new ApiFactory(_directory, creatorWritesPerHour: 3);
        var (_, token) = await CreatorTestData.NewCreatorAsync(factory, "Limit Anna");

        var client = factory.CreateClient();

        for (var i = 1; i <= 3; i++)
        {
            var accepted = await CreateWishListAsync(client, token, $"Liste {i}");
            Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        }

        var refused = await CreateWishListAsync(client, token, "Eine zu viel");

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);

        var body = await refused.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("too_many_requests", body.GetProperty("code").GetString());

        // FR-036: the refusal says when to try again. A refusal that says only "no" leaves the
        // holder guessing whether to retry in a second or an hour.
        Assert.True(body.GetProperty("retryAfterSeconds").GetInt32() > 0);
    }

    [Fact]
    public async Task A_refused_write_changed_nothing()
    {
        using var factory = new ApiFactory(_directory, creatorWritesPerHour: 1);
        var (_, token) = await CreatorTestData.NewCreatorAsync(factory, "Nichts Anna");

        var client = factory.CreateClient();

        (await CreateWishListAsync(client, token, "Die erste")).EnsureSuccessStatusCode();
        var refused = await CreateWishListAsync(client, token, "Die verworfene");
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);

        var surface = await factory.CreateClient()
            .GetFromJsonAsync<JsonElement>($"/api/v1/e/{token}");

        var titles = surface.GetProperty("wishLists").EnumerateArray()
            .Select(l => l.GetProperty("title").GetString()!)
            .ToArray();

        Assert.Equal(new[] { "Die erste" }, titles);
    }

    [Fact]
    public async Task Reading_is_never_refused_however_often_a_holder_refreshes()
    {
        // FR-036c. A holder refreshing their own list must never be told to come back later; the
        // budget exists to bound what a leaked link can do, not to ration a legitimate holder.
        using var factory = new ApiFactory(_directory, creatorWritesPerHour: 1);
        var (_, token) = await CreatorTestData.NewCreatorAsync(factory, "Lesen Anna");

        var client = factory.CreateClient();

        for (var i = 0; i < 20; i++)
        {
            var response = await client.GetAsync($"/api/v1/e/{token}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task The_creator_budget_and_the_participant_budget_are_separate_buckets()
    {
        // A framework behaviour this code depends on and does not own: ASP.NET Core holds rate
        // limiter partitions per policy, so the same address has one budget for submissions and
        // another for creator writes.
        //
        // It matters concretely. Without it, an operator answering their own poll from the same
        // machine would spend an Ersteller's allowance, and the two limits would interfere in a
        // way neither specification describes (research R-5).
        using var factory = new ApiFactory(_directory, submissionsPerHour: 1, creatorWritesPerHour: 1);
        var (_, token) = await CreatorTestData.NewCreatorAsync(factory, "Getrennt Anna");

        var client = factory.CreateClient();

        // Spend the participant budget by answering a poll.
        var pollId = await CreatorTestData.NewPollAsync(client, token);

        var surface = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/e/{token}");
        var participantToken = surface.GetProperty("polls").EnumerateArray()
            .Single(p => p.GetProperty("id").GetGuid() == pollId)
            .GetProperty("participantToken").GetString();

        var poll = await factory.CreateClient()
            .GetFromJsonAsync<JsonElement>($"/api/v1/polls/{participantToken}");
        var dayId = poll.GetProperty("days").EnumerateArray().First().GetProperty("id").GetGuid();

        var participant = factory.CreateClient();
        var firstAnswer = await participant.PostAsJsonAsync($"/api/v1/polls/{participantToken}/responses", new
        {
            displayName = "Teilnehmer",
            answers = new[] { new { dayId, availability = "yes" } },
        });
        Assert.Equal(HttpStatusCode.Created, firstAnswer.StatusCode);

        var secondAnswer = await participant.PostAsJsonAsync($"/api/v1/polls/{participantToken}/responses", new
        {
            displayName = "Noch einer",
            answers = new[] { new { dayId, availability = "yes" } },
        });
        Assert.Equal(HttpStatusCode.TooManyRequests, secondAnswer.StatusCode);

        // The participant budget is spent. The creator budget was spent by the poll creation
        // above and not by either answer - so exactly one more creator write is refused, and it is
        // refused by its own limiter rather than by the participant's.
        var creatorWrite = await CreateWishListAsync(client, token, "Nach den Antworten");
        Assert.Equal(HttpStatusCode.TooManyRequests, creatorWrite.StatusCode);
    }

    [Fact]
    public async Task There_is_no_cap_on_how_much_one_Ersteller_may_own()
    {
        // FR-036a. Creation is invited activity - the operator handed the link over precisely so
        // that things would be created - so nothing counts what has accumulated. The only enforced
        // maxima on content are features 002's and 008's per-item limits.
        using var factory = new ApiFactory(_directory, creatorWritesPerHour: 1000);
        var (_, token) = await CreatorTestData.NewCreatorAsync(factory, "Viele Anna");

        var client = factory.CreateClient();

        for (var i = 1; i <= 25; i++)
        {
            var response = await CreateWishListAsync(client, token, $"Liste {i}");
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var surface = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/e/{token}");

        Assert.Equal(25, surface.GetProperty("wishLists").EnumerateArray().Count());
    }

    [Fact]
    public void The_default_budget_is_the_sixty_the_specification_fixes()
    {
        // The constant, pinned separately from the configurability above. Six times the
        // participant's ten, because a participant submits once and an Ersteller building a wish
        // list with ten items spends ten writes on their first task (FR-036).
        Assert.Equal(60, Rundfrage.Api.Http.RateLimiting.DefaultCreatorWritesPerWindow);
        Assert.Equal(10, Rundfrage.Api.Http.RateLimiting.DefaultPermitsPerWindow);
    }
}
