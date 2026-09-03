using Rundfrage.Api.Security;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// FR-017 and SC-006. Under Principle I a token is the only thing standing between a stranger
/// and a poll, because there is no account to check instead.
/// </summary>
public class CapabilityTokenTests
{
    [Fact]
    public void Is_twenty_two_base64url_characters()
    {
        var token = CapabilityToken.Mint();

        Assert.Equal(22, token.Length);
        Assert.Matches("^[A-Za-z0-9_-]{22}$", token);
    }

    [Fact]
    public void Carries_at_least_the_required_entropy()
    {
        // SC-006 sets a floor of 2^120; 16 bytes gives 2^128.
        Assert.True(CapabilityToken.EntropyBits >= 120);
    }

    [Fact]
    public void Ten_thousand_mints_produce_ten_thousand_distinct_tokens()
    {
        var tokens = Enumerable.Range(0, 10_000).Select(_ => CapabilityToken.Mint()).ToHashSet();

        Assert.Equal(10_000, tokens.Count);
    }

    [Fact]
    public void Successive_tokens_share_no_common_prefix()
    {
        // FR-017 forbids deriving a token from a counter. A sequential source would show up as
        // a long shared prefix between consecutive values.
        var first = CapabilityToken.Mint();
        var second = CapabilityToken.Mint();

        var shared = first.Zip(second).TakeWhile(pair => pair.First == pair.Second).Count();

        Assert.True(shared <= 2, $"tokens shared a {shared}-character prefix; expected effectively none");
    }

    [Fact]
    public void Is_not_derived_from_any_input()
    {
        // The mint takes no argument at all, so a token cannot encode the title, the days, or
        // anything else about the poll (FR-017).
        Assert.Empty(typeof(CapabilityToken).GetMethod(nameof(CapabilityToken.Mint))!.GetParameters());
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("this-token-is-far-too-long-to-be-valid")]
    [InlineData("contains spaces at all!")]
    [InlineData("plus+slash/notbase64url")]
    public void Rejects_anything_that_is_not_a_token(string candidate)
    {
        Assert.False(CapabilityToken.IsWellFormed(candidate));
    }

    [Fact]
    public void Accepts_what_it_mints()
    {
        Assert.True(CapabilityToken.IsWellFormed(CapabilityToken.Mint()));
    }
}
