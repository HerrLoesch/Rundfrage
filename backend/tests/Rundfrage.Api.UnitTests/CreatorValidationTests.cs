using Rundfrage.Api.Creators;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Http;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// 009 FR-001 to FR-003: what an Ersteller's name must be, checked without a database.
/// </summary>
/// <remarks>
/// Pure, for the same reason <c>PollService.Validate</c> and <c>WishListService.ValidateList</c>
/// are pure: so there is exactly one place where "enforced on the server" is true, and so it can
/// be exercised without standing anything up.
/// </remarks>
public class CreatorValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void A_name_is_required(string? name)
    {
        var error = CreatorService.ValidateName(name);

        Assert.Equal(ErrorCodes.CreatorNameRequired, error?.Code);
    }

    [Fact]
    public void A_name_at_the_limit_is_accepted()
    {
        Assert.Null(CreatorService.ValidateName(new string('a', Creator.NameMaxLength)));
    }

    [Fact]
    public void A_name_beyond_the_limit_is_refused_and_says_the_limit()
    {
        var error = CreatorService.ValidateName(new string('a', Creator.NameMaxLength + 1));

        Assert.Equal(ErrorCodes.CreatorNameTooLong, error?.Code);

        // The number is what makes the refusal actionable. A refusal that says only "too long"
        // is one the operator cannot act on.
        Assert.Equal(Creator.NameMaxLength, error?.Limit);
    }

    [Fact]
    public void An_ordinary_name_is_accepted()
    {
        Assert.Null(CreatorService.ValidateName("Anna"));
    }

    [Fact]
    public void The_limit_is_the_hundred_the_specification_names()
    {
        // FR-009. An enforced maximum, unlike the documented scales of 007 FR-028c and
        // 008 FR-054 - so it is pinned here rather than left to whatever the code happens to say.
        Assert.Equal(100, Creator.MaxCreators);
        Assert.Equal(100, Creator.NameMaxLength);
    }
}
