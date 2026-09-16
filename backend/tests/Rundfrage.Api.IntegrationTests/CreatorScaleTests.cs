using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Security;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 009 SC-010: an Ersteller's own lists within two seconds at the documented scale, and the
/// operator's areas still inside the budgets features 007 and 008 set.
/// </summary>
/// <remarks>
/// <b>No spike was needed before design and this is not one.</b> This feature adds a single indexed
/// equality predicate to queries that already met their budgets, and it reduces the number of rows
/// each one returns (research R-14). The test exists because "should be faster" is not a
/// measurement, and because the owner filter is the one new thing on the hot path.
/// <para>
/// A hundred Ersteller is the enforced maximum (FR-009), so this is the real worst case for the
/// creator surface rather than a documented approximation of one. The content per Ersteller is
/// deliberately modest: FR-036b leaves the installation-wide totals of 007 FR-028c and 008 FR-054
/// unchanged, so what this feature changes is who reaches them, not how many there are.
/// </para>
/// </remarks>
public sealed class CreatorScaleTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    private const int Creators = Creator.MaxCreators;
    private const int PollsEach = 4;
    private const int ListsEach = 4;

    private async Task<string> SeedAsync()
    {
        await using var db = new RundfrageDbContext(
            new DbContextOptionsBuilder<RundfrageDbContext>()
                .UseSqlite(StorageLocation.ConnectionStringFor(storage.DataDirectory))
                .AddInterceptors(StorageSetup.Interceptor)
                .Options);

        await db.Database.MigrateAsync();

        if (await db.Creators.CountAsync() >= Creators)
        {
            return await db.Creators.Select(c => c.LinkToken!).FirstAsync();
        }

        var deadline = DateTime.UtcNow.AddDays(120);
        string? first = null;

        for (var i = 0; i < Creators; i++)
        {
            var creator = new Creator
            {
                Id = Guid.CreateVersion7(),
                Name = $"Ersteller {i:D3}",
                LinkToken = CapabilityToken.Mint(),
                CreatedAt = DateTime.UtcNow,
            };
            first ??= creator.LinkToken;
            db.Creators.Add(creator);

            for (var p = 0; p < PollsEach; p++)
            {
                db.Polls.Add(new Poll
                {
                    Id = Guid.CreateVersion7(),
                    Title = $"Termin {i}-{p}",
                    ParticipantToken = CapabilityToken.Mint(),
                    CreatedAt = DateTime.UtcNow,
                    RetentionDeadline = deadline,
                    CreatorId = creator.Id,
                });
            }

            for (var w = 0; w < ListsEach; w++)
            {
                db.WishLists.Add(new WishList
                {
                    Id = Guid.CreateVersion7(),
                    Title = $"Liste {i}-{w}",
                    TargetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                    ListToken = CapabilityToken.Mint(),
                    CreatedAt = DateTime.UtcNow,
                    CreatorId = creator.Id,
                });
            }
        }

        await db.SaveChangesAsync();

        return first!;
    }

    [Fact]
    public async Task An_Ersteller_sees_their_own_lists_within_the_budget()
    {
        var token = await SeedAsync();
        using var factory = new ApiFactory(storage.DataDirectory);
        var client = factory.CreateClient();

        // One warm call first, so the measurement is of the query rather than of the host starting.
        (await client.GetAsync($"/api/v1/e/{token}")).EnsureSuccessStatusCode();

        var clock = Stopwatch.StartNew();
        var response = await client.GetAsync($"/api/v1/e/{token}");
        clock.Stop();

        response.EnsureSuccessStatusCode();

        Assert.True(
            clock.Elapsed < Budget,
            $"the creator surface took {clock.ElapsedMilliseconds} ms at {Creators} Ersteller "
            + $"(budget {Budget.TotalMilliseconds} ms)");
    }

    [Fact]
    public async Task The_operators_areas_stay_inside_the_budgets_features_007_and_008_set()
    {
        await SeedAsync();
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        foreach (var path in new[] { "/api/v1/admin/polls", "/api/v1/admin/wish-lists", "/api/v1/admin/creators" })
        {
            (await admin.GetAsync(path)).EnsureSuccessStatusCode();

            var clock = Stopwatch.StartNew();
            var response = await admin.GetAsync(path);
            clock.Stop();

            response.EnsureSuccessStatusCode();

            Assert.True(
                clock.Elapsed < Budget,
                $"{path} took {clock.ElapsedMilliseconds} ms (budget {Budget.TotalMilliseconds} ms)");
        }
    }

    [Fact]
    public async Task The_Ersteller_area_reads_its_counts_without_a_query_per_row()
    {
        // FR-044's two counts, at the limit. The shape that would fail here is a count per
        // Ersteller - a hundred round trips inside one request, which is the trap 007 FR-028b
        // names for the dashboard and which CreatorProjection avoids by grouping in the database.
        await SeedAsync();
        using var factory = new ApiFactory(storage.DataDirectory);
        var admin = await factory.CreateSignedInClientAsync();

        (await admin.GetAsync("/api/v1/admin/creators")).EnsureSuccessStatusCode();

        var clock = Stopwatch.StartNew();
        var body = await admin.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/admin/creators");
        clock.Stop();

        Assert.Equal(Creators, body.EnumerateArray().Count());
        Assert.All(
            body.EnumerateArray(),
            row => Assert.Equal(PollsEach, row.GetProperty("pollCount").GetInt32()));

        Assert.True(
            clock.Elapsed < Budget,
            $"the Ersteller area took {clock.ElapsedMilliseconds} ms at {Creators} rows");
    }
}
