using Microsoft.Extensions.Configuration;
using Rundfrage.Api.Http;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// FR-027a. The number is configurable so a test environment can raise it, but an unconfigured
/// deployment must still get the ten the specification requires.
/// </summary>
public class RateLimitConfigurationTests
{
    private static IConfiguration With(string? value) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [RateLimiting.PermitsVariable] = value,
            })
            .Build();

    [Fact]
    public void Defaults_to_the_ten_the_specification_requires()
    {
        Assert.Equal(10, RateLimiting.PermitsPerWindow(With(null)));
        Assert.Equal(RateLimiting.DefaultPermitsPerWindow, RateLimiting.PermitsPerWindow(With(null)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("-5")]
    public void Falls_back_to_the_default_for_anything_unusable(string configured)
    {
        // A typo must not silently disable the limit - that would turn a configuration mistake
        // into an open door.
        Assert.Equal(RateLimiting.DefaultPermitsPerWindow, RateLimiting.PermitsPerWindow(With(configured)));
    }

    [Fact]
    public void Honours_a_configured_value()
    {
        Assert.Equal(1000, RateLimiting.PermitsPerWindow(With("1000")));
    }
}
