using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Retention;
using Rundfrage.Api.Security;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// 008 FR-037: nothing removes a wish list except the operator.
/// </summary>
/// <remarks>
/// <b>This test passes the day it is written, and that is the point.</b> It is a guard, not a
/// specification of new behaviour: the plausible future mistake is somebody generalising
/// <see cref="RetentionService"/> - "polls expire, so surely wish lists do too" - or adding wish
/// lists to the hourly sweep while tidying up. Either would silently destroy data that the
/// specification promises to keep until the operator says otherwise. If that happens, this fails.
/// <para>
/// The wish list here carries a target date far in the past, because that is the value somebody
/// would most plausibly mistake for an expiry date. A list whose day is long gone is closed
/// (FR-028a) and still entirely present (FR-028).
/// </para>
/// </remarks>
public class WishRetentionTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private static async Task<Guid> StoreAWishListAsync(ApiFactory factory, DateOnly targetDate)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();

        var list = new WishList
        {
            Id = Guid.CreateVersion7(),
            Title = "Sommerfest",
            TargetDate = targetDate,
            ListToken = CapabilityToken.Mint(),
            CreatedAt = DateTime.UtcNow,
            Items =
            [
                new WishItem
                {
                    Id = Guid.CreateVersion7(),
                    Name = "Kuchen",
                    WantedCount = 2,
                    Position = 0,
                    Claims =
                    [
                        new WishClaim
                        {
                            Id = Guid.CreateVersion7(),
                            DisplayName = "Anna",
                            ClaimToken = CapabilityToken.Mint(),
                            SubmittedAt = DateTime.UtcNow,
                        },
                    ],
                },
            ],
        };

        db.WishLists.Add(list);
        await db.SaveChangesAsync();

        return list.Id;
    }

    [Fact]
    public async Task The_retention_sweep_leaves_wish_lists_alone()
    {
        using var factory = new ApiFactory(storage.DataDirectory);

        // Two years in the past: long closed, and long past anything that could be mistaken for
        // a retention deadline.
        var listId = await StoreAWishListAsync(factory, DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)));

        using var scope = factory.Services.CreateScope();
        var retention = scope.ServiceProvider.GetRequiredService<RetentionService>();
        await retention.EraseExpiredAsync(CancellationToken.None);

        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();

        Assert.True(await db.WishLists.AnyAsync(l => l.Id == listId));
        Assert.True(await db.WishItems.AnyAsync(i => i.WishListId == listId));
        Assert.True(await db.WishClaims.AnyAsync(c => c.WishItem!.WishListId == listId));
    }

    [Fact]
    public async Task A_wish_list_has_nothing_resembling_a_deletion_date()
    {
        // FR-037 in the model rather than in behaviour: if a retention or expiry column ever
        // appears on WishLists, the sweep above is one refactoring away from using it.
        using var factory = new ApiFactory(storage.DataDirectory);
        await StoreAWishListAsync(factory, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RundfrageDbContext>();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT lower(name) FROM pragma_table_info('WishLists')";

        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        foreach (var forbidden in new[] { "retentiondeadline", "expiresat", "deleteat", "closedat" })
        {
            Assert.DoesNotContain(forbidden, columns);
        }
    }
}
