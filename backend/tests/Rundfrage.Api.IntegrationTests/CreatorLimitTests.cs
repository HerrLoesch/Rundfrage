using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Http;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 009 FR-009 and FR-009a: a hundred Ersteller, and which action frees a place.
/// </summary>
/// <remarks>
/// Driven against the service rather than the endpoint, because the interesting half - two
/// requests reaching the hundredth place at once - cannot be staged reliably through HTTP and does
/// not need to be. The cap is a read-modify-write, and SQLite's default deferred transaction takes
/// its lock too late to make one safe (research R-11).
/// </remarks>
public sealed class CreatorLimitTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private async Task<CreatorServices> StartAsync()
    {
        var services = await CreatorServices.StartAsync(storage.DataDirectory);
        await services.Db.Creators.ExecuteDeleteAsync();
        return services;
    }

    [Fact]
    public async Task The_hundredth_is_created_and_the_hundred_and_first_is_refused()
    {
        await using var services = await StartAsync();

        for (var i = 1; i <= Creator.MaxCreators; i++)
        {
            var (created, error) = await services.Creators.CreateAsync($"Ersteller {i}", default);
            Assert.Null(error);
            Assert.NotNull(created);
        }

        var (none, refused) = await services.Creators.CreateAsync("Einer zu viel", default);

        Assert.Null(none);
        Assert.Equal(ErrorCodes.CreatorLimitReached, refused?.Code);

        // The number is what the interface renders into the sentence that matters: a place is
        // freed by deleting an Ersteller, not by revoking one (FR-009a).
        Assert.Equal(Creator.MaxCreators, refused?.Limit);
        Assert.Equal(Creator.MaxCreators, await services.Db.Creators.CountAsync());
    }

    [Fact]
    public async Task Revoking_does_not_free_a_place_and_deleting_does()
    {
        // The distinction the spec's first clarification turns on, at the point where an operator
        // meets it: somebody at the limit who revokes links to make room gets nowhere.
        await using var services = await StartAsync();

        var made = new List<Creator>();
        for (var i = 1; i <= Creator.MaxCreators; i++)
        {
            var (created, _) = await services.Creators.CreateAsync($"Voll {i}", default);
            made.Add(created!);
        }

        Assert.True(await services.Creators.RevokeAsync(made[0].Id, default));

        var (stillNone, stillRefused) = await services.Creators.CreateAsync("Nach dem Sperren", default);
        Assert.Null(stillNone);
        Assert.Equal(ErrorCodes.CreatorLimitReached, stillRefused?.Code);

        Assert.True(await services.Creators.DeleteAsync(made[1].Id, default));

        var (accepted, noError) = await services.Creators.CreateAsync("Nach dem Löschen", default);
        Assert.Null(noError);
        Assert.NotNull(accepted);
    }

    [Fact]
    public async Task A_name_is_taken_while_the_Ersteller_exists_and_free_once_it_is_deleted()
    {
        // FR-002. A revoked Ersteller still exists and still owns its content, so its name is
        // still taken; only deleting releases it.
        await using var services = await StartAsync();

        var (anna, _) = await services.Creators.CreateAsync("Anna", default);

        var (none, duplicate) = await services.Creators.CreateAsync("anna", default);
        Assert.Null(none);
        Assert.Equal(ErrorCodes.CreatorNameDuplicate, duplicate?.Code);

        // Named, so an operator who typed it knows which one they collided with.
        Assert.Equal("Anna", duplicate?.Detail);

        await services.Creators.RevokeAsync(anna!.Id, default);
        var (stillNone, stillDuplicate) = await services.Creators.CreateAsync("ANNA", default);
        Assert.Null(stillNone);
        Assert.Equal(ErrorCodes.CreatorNameDuplicate, stillDuplicate?.Code);

        await services.Creators.DeleteAsync(anna.Id, default);
        var (reused, noError) = await services.Creators.CreateAsync("Anna", default);
        Assert.Null(noError);
        Assert.NotNull(reused);
    }

    [Fact]
    public async Task Concurrent_creation_at_the_boundary_never_produces_a_hundred_and_one()
    {
        // The reason the cap lives inside an immediate write transaction. Counting then inserting
        // is a read-modify-write, and SQLite's default *deferred* transaction takes its lock at the
        // first write rather than the first statement - so two callers can both count ninety-nine
        // (research R-11).
        //
        // The barrier is not decoration. An earlier version of this test simply started eight
        // tasks, and it passed with `deferred: true` as happily as with `deferred: false`: the
        // calls are short enough that they never overlapped, so it proved nothing. Holding every
        // racer until all of them are ready is what makes the window real.
        await using var seed = await StartAsync();

        for (var i = 1; i <= Creator.MaxCreators - 1; i++)
        {
            await seed.Creators.CreateAsync($"Fast voll {i}", default);
        }

        const int racers = 8;
        using var ready = new Barrier(racers);

        // Separate contexts, because one DbContext is not thread-safe and sharing one would test
        // that fact instead of the transaction.
        var attempts = Enumerable.Range(0, racers).Select(i => Task.Run(async () =>
        {
            await using var services = new CreatorServices(storage.DataDirectory);

            // Open the connection before the barrier, so the thing being raced is the
            // count-then-insert and not the connection handshake.
            await services.Db.Database.OpenConnectionAsync();

            ready.SignalAndWait();

            try
            {
                var (created, error) = await services.Creators.CreateAsync($"Rennen {i}", default);
                return (Created: created is not null, Refused: error?.Code, Threw: (string?)null);
            }
            catch (Exception failure)
            {
                // A racer that throws is a failure of this design, not an acceptable outcome: the
                // point of an immediate transaction is that contention *queues* rather than
                // erroring. Captured rather than thrown so the assertions below can say so.
                return (Created: false, Refused: null, Threw: failure.GetType().Name);
            }
        }));

        var results = await Task.WhenAll(attempts);

        Assert.All(results, r => Assert.Null(r.Threw));
        Assert.Equal(1, results.Count(r => r.Created));
        Assert.Equal(racers - 1, results.Count(r => r.Refused == ErrorCodes.CreatorLimitReached));

        await using var verify = new CreatorServices(storage.DataDirectory);
        Assert.Equal(Creator.MaxCreators, await verify.Db.Creators.CountAsync());
    }
}
