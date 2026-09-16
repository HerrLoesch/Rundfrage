using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Security;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 009 FR-032 and FR-033: an Ersteller sees their own and nothing else.
/// </summary>
/// <remarks>
/// <b>The feature's central safety property, asserted at the filter and before any endpoint
/// exists.</b> <c>CreatorIsolationTests</c> proves the same promise through HTTP on every route;
/// this proves it where it is actually enforced. Isolation written first and tested later is
/// isolation retrofitted, and its failure mode is a leak that looks like a working feature.
/// </remarks>
public sealed class OwnerScopeTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private async Task<(CreatorServices Services, Creator Anna, Creator Ben)> StageAsync()
    {
        var services = await CreatorServices.StartAsync(storage.DataDirectory);

        await services.Db.Polls.ExecuteDeleteAsync();
        await services.Db.WishLists.ExecuteDeleteAsync();
        await services.Db.Creators.ExecuteDeleteAsync();

        var (anna, _) = await services.Creators.CreateAsync("Anna", default);
        var (ben, _) = await services.Creators.CreateAsync("Ben", default);

        // One of each for Anna, one of each for Ben, and one of each for the operator - whose
        // ownership is a null CreatorId rather than a row of their own (research R-2).
        AddPoll(services, "Annas Termin", anna!.Id, DateTime.UtcNow.AddDays(30));
        AddPoll(services, "Bens Termin", ben!.Id, DateTime.UtcNow.AddDays(30));
        AddPoll(services, "Termin des Betreibers", null, DateTime.UtcNow.AddDays(30));

        AddWishList(services, "Annas Liste", anna.Id);
        AddWishList(services, "Bens Liste", ben.Id);
        AddWishList(services, "Liste des Betreibers", null);

        await services.Db.SaveChangesAsync();

        return (services, anna, ben);
    }

    private static void AddPoll(CreatorServices services, string title, Guid? owner, DateTime deadline) =>
        services.Db.Polls.Add(new Poll
        {
            Id = Guid.CreateVersion7(),
            Title = title,
            ParticipantToken = CapabilityToken.Mint(),
            CreatedAt = DateTime.UtcNow,
            RetentionDeadline = deadline,
            CreatorId = owner,
        });

    private static void AddWishList(CreatorServices services, string title, Guid? owner) =>
        services.Db.WishLists.Add(new WishList
        {
            Id = Guid.CreateVersion7(),
            Title = title,
            TargetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            ListToken = CapabilityToken.Mint(),
            CreatedAt = DateTime.UtcNow,
            CreatorId = owner,
        });

    [Fact]
    public async Task An_Ersteller_sees_their_own_polls_and_nobody_elses()
    {
        var (services, anna, _) = await StageAsync();
        await using var _services = services;

        var titles = await services.ScopeFor(anna).Polls().Select(p => p.Title).ToListAsync();

        Assert.Equal(["Annas Termin"], titles);
    }

    [Fact]
    public async Task An_Ersteller_sees_their_own_wish_lists_and_nobody_elses()
    {
        var (services, anna, _) = await StageAsync();
        await using var _services = services;

        var titles = await services.ScopeFor(anna).WishLists().Select(l => l.Title).ToListAsync();

        Assert.Equal(["Annas Liste"], titles);
    }

    [Fact]
    public async Task The_operators_own_content_is_invisible_to_every_Ersteller()
    {
        // FR-032. The operator's rows carry a null CreatorId, and SQL's three-valued logic is
        // exactly why no handler writes that predicate by hand: `CreatorId != @id` would have
        // *excluded* these rows from a query meant to exclude them, which happens to be right -
        // and `CreatorId == null || ...` written by somebody in a hurry would have included them.
        var (services, anna, ben) = await StageAsync();
        await using var _services = services;

        foreach (var owner in new[] { anna, ben })
        {
            var polls = await services.ScopeFor(owner).Polls().Select(p => p.Title).ToListAsync();
            var lists = await services.ScopeFor(owner).WishLists().Select(l => l.Title).ToListAsync();

            Assert.DoesNotContain("Termin des Betreibers", polls);
            Assert.DoesNotContain("Liste des Betreibers", lists);
        }
    }

    [Fact]
    public async Task One_Ersteller_never_reaches_another_by_naming_an_identifier()
    {
        // FR-033: scoping is enforced where the data is read. Asking for Ben's poll by its id
        // through Anna's scope returns nothing - not a forbidden result, nothing at all, which is
        // what lets the endpoint answer the same neutral payload an unknown id answers (FR-034).
        var (services, anna, ben) = await StageAsync();
        await using var _services = services;

        var bensPoll = await services.ScopeFor(ben).Polls().SingleAsync();
        var bensList = await services.ScopeFor(ben).WishLists().SingleAsync();

        Assert.Null(await services.ScopeFor(anna).Polls()
            .FirstOrDefaultAsync(p => p.Id == bensPoll.Id));
        Assert.Null(await services.ScopeFor(anna).WishLists()
            .FirstOrDefaultAsync(l => l.Id == bensList.Id));
    }

    [Fact]
    public async Task An_expired_poll_is_as_invisible_to_its_Ersteller_as_it_is_to_the_operator()
    {
        // FR-014: ownership changes nothing about retention. This is why Polls() composes with
        // RetentionService.LivePolls() rather than querying db.Polls directly - without it, an
        // Ersteller would be the only person in the system who can still see an expired poll.
        var (services, anna, _) = await StageAsync();
        await using var _services = services;

        AddPoll(services, "Annas abgelaufener Termin", anna.Id, DateTime.UtcNow.AddDays(-1));
        await services.Db.SaveChangesAsync();

        var titles = await services.ScopeFor(anna).Polls().Select(p => p.Title).ToListAsync();

        Assert.Equal(["Annas Termin"], titles);
    }

    [Fact]
    public async Task A_wish_list_is_never_filtered_by_a_date()
    {
        // The mirror of the test above, and deliberately the opposite: nothing expires a wish list
        // (008 FR-037), so a target date long past must leave it exactly where it is. A retention
        // filter copied over from Polls() would have quietly hidden it.
        var (services, anna, _) = await StageAsync();
        await using var _services = services;

        services.Db.WishLists.Add(new WishList
        {
            Id = Guid.CreateVersion7(),
            Title = "Annas alte Liste",
            TargetDate = new DateOnly(2020, 1, 1),
            ListToken = CapabilityToken.Mint(),
            CreatedAt = DateTime.UtcNow,
            CreatorId = anna.Id,
        });
        await services.Db.SaveChangesAsync();

        var titles = await services.ScopeFor(anna).WishLists().Select(l => l.Title).ToListAsync();

        Assert.Contains("Annas alte Liste", titles);
        Assert.Contains("Annas Liste", titles);
        Assert.Equal(2, titles.Count);
    }

    [Fact]
    public void Reaching_the_owner_before_a_token_resolved_is_a_programming_error()
    {
        // Not an authorisation failure - there is no request here to refuse. It throws rather than
        // returning something permissive, because an unbound scope that quietly returned
        // everything is the shape this whole class exists to prevent.
        var scope = new Rundfrage.Api.Creators.OwnerScope(
            new CreatorServices(storage.DataDirectory).Db,
            new CreatorServices(storage.DataDirectory).Retention);

        Assert.Throws<InvalidOperationException>(() => scope.Polls());
        Assert.Throws<InvalidOperationException>(() => scope.WishLists());
    }
}
