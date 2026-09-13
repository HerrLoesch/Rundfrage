using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Http;
using Rundfrage.Api.Wishes;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// The limits of 008 FR-010 and the creation rules of FR-001 to FR-008, checked without a
/// database so that "enforced on the server" has exactly one place where it is true.
/// </summary>
public class WishListValidationTests
{
    private static readonly DateOnly Target = new(2026, 7, 18);

    private static WishItemDraft Item(string name = "Kuchen", int? wanted = null) =>
        new(name, wanted);

    private static WishItemDraft[] Items(int count) =>
        [.. Enumerable.Range(0, count).Select(i => Item($"Sache {i}"))];

    [Fact]
    public void A_title_is_required()
    {
        Assert.Equal(
            ErrorCodes.TitleRequired,
            WishListService.ValidateList(null, null, Target, [Item()])?.Code);

        Assert.Equal(
            ErrorCodes.TitleRequired,
            WishListService.ValidateList("   ", null, Target, [Item()])?.Code);
    }

    [Fact]
    public void A_title_has_a_limit_and_the_refusal_names_it()
    {
        var error = WishListService.ValidateList(
            new string('x', WishList.TitleMaxLength + 1), null, Target, [Item()]);

        Assert.Equal(ErrorCodes.TitleTooLong, error?.Code);
        Assert.Equal(WishList.TitleMaxLength, error?.Limit);
    }

    [Fact]
    public void A_description_has_a_limit()
    {
        var error = WishListService.ValidateList(
            "Sommerfest", new string('x', WishList.DescriptionMaxLength + 1), Target, [Item()]);

        Assert.Equal(ErrorCodes.DescriptionTooLong, error?.Code);
        Assert.Equal(WishList.DescriptionMaxLength, error?.Limit);
    }

    [Fact]
    public void A_missing_description_is_not_a_defect()
    {
        // FR-003: its absence must not prevent creation.
        Assert.Null(WishListService.ValidateList("Sommerfest", null, Target, [Item()]));
        Assert.Null(WishListService.ValidateList("Sommerfest", "   ", Target, [Item()]));
    }

    [Fact]
    public void A_target_date_is_required()
    {
        Assert.Equal(
            ErrorCodes.TargetDateRequired,
            WishListService.ValidateList("Sommerfest", null, null, [Item()])?.Code);
    }

    [Fact]
    public void A_target_date_in_the_past_is_permitted()
    {
        // FR-002a: a list may be created for an occasion already under way. Validation knows
        // nothing about the clock; whether the list is then closed is a separate question.
        Assert.Null(WishListService.ValidateList(
            "Sommerfest", null, new DateOnly(2020, 1, 1), [Item()]));
    }

    [Fact]
    public void At_least_one_item_is_required()
    {
        Assert.Equal(
            ErrorCodes.ItemsRequired,
            WishListService.ValidateList("Sommerfest", null, Target, [])?.Code);
    }

    [Fact]
    public void An_item_needs_a_name()
    {
        Assert.Equal(
            ErrorCodes.ItemNameRequired,
            WishListService.ValidateList("Sommerfest", null, Target, [Item("  ")])?.Code);
    }

    [Fact]
    public void An_item_name_has_a_limit()
    {
        var error = WishListService.ValidateList(
            "Sommerfest", null, Target, [Item(new string('x', WishItem.NameMaxLength + 1))]);

        Assert.Equal(ErrorCodes.ItemNameTooLong, error?.Code);
        Assert.Equal(WishItem.NameMaxLength, error?.Limit);
    }

    [Fact]
    public void Two_items_may_not_share_a_name()
    {
        // FR-008. Case and surrounding space do not make a second thing out of one.
        Assert.Equal(
            ErrorCodes.DuplicateItemName,
            WishListService.ValidateList(
                "Sommerfest", null, Target, [Item("Kuchen"), Item(" kuchen ")])?.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(WishItem.MaxWantedCount + 1)]
    public void A_wanted_count_outside_its_range_is_refused(int wanted)
    {
        var error = WishListService.ValidateList(
            "Sommerfest", null, Target, [Item("Kuchen", wanted)]);

        Assert.Equal(ErrorCodes.WantedCountInvalid, error?.Code);
        Assert.Equal(WishItem.MaxWantedCount, error?.Limit);
    }

    [Fact]
    public void An_item_with_no_stated_number_is_wanted_once()
    {
        // FR-006, and the default lives here rather than only in the form.
        Assert.Null(WishListService.ValidateList("Sommerfest", null, Target, [Item("Kuchen")]));
        Assert.Equal(1, WishListService.WantedCountOf(Item("Kuchen")));
        Assert.Equal(3, WishListService.WantedCountOf(Item("Kuchen", 3)));
    }

    [Fact]
    public void A_list_may_not_hold_more_items_than_the_limit()
    {
        var error = WishListService.ValidateList(
            "Sommerfest", null, Target, Items(WishList.MaxItems + 1));

        Assert.Equal(ErrorCodes.TooManyItems, error?.Code);
        Assert.Equal(WishList.MaxItems, error?.Limit);
    }

    [Fact]
    public void The_wished_places_of_a_list_are_capped_and_the_refusal_says_what_is_left()
    {
        // FR-010a: the operator meets this limit where they work, and the number that makes it
        // actionable travels with the refusal. 21 items wanted 50 times each is 1050 places.
        var items = Enumerable.Range(0, 21)
            .Select(i => new WishItemDraft($"Sache {i}", WishItem.MaxWantedCount))
            .ToArray();

        var error = WishListService.ValidateList("Sommerfest", null, Target, items);

        Assert.Equal(ErrorCodes.TooManyPlaces, error?.Code);

        // Nothing is stored yet, so the whole capacity is what remains.
        Assert.Equal(WishList.MaxPlaces, error?.Limit);
    }

    [Fact]
    public void Exactly_the_capacity_is_allowed()
    {
        var items = Enumerable.Range(0, 20)
            .Select(i => new WishItemDraft($"Sache {i}", WishItem.MaxWantedCount))
            .ToArray();

        Assert.Null(WishListService.ValidateList("Sommerfest", null, Target, items));
    }

    [Theory]
    [InlineData(3, 3, null)]                              // lowering to exactly the claims made
    [InlineData(3, 4, null)]                              // raising
    [InlineData(3, 2, ErrorCodes.CountBelowEntries)]      // below the claims made
    public void A_wanted_count_may_not_drop_below_the_claims_already_made(
        int claims, int newCount, string? expected)
    {
        // FR-033. The refusal carries the number of claims, so the operator knows what to remove
        // first rather than being told only that they may not.
        var error = WishListService.ValidateItemCount(
            newCount, claimsOnItem: claims, placesElsewhere: 0);

        Assert.Equal(expected, error?.Code);

        if (expected is not null)
        {
            Assert.Equal(claims, error?.Limit);
        }
    }

    [Fact]
    public void Raising_a_count_past_the_list_capacity_is_refused_with_the_headroom()
    {
        // 990 places already on other items leaves 10; asking for 20 is refused, naming the 10.
        var error = WishListService.ValidateItemCount(
            20, claimsOnItem: 0, placesElsewhere: WishList.MaxPlaces - 10);

        Assert.Equal(ErrorCodes.TooManyPlaces, error?.Code);
        Assert.Equal(10, error?.Limit);
    }
}
