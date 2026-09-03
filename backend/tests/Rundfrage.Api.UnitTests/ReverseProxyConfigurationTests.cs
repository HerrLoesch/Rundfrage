using Microsoft.Extensions.Configuration;
using Rundfrage.Api.Http;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// Believing <c>X-Forwarded-*</c> is a decision with a cost, so it is off until the operator
/// states how many proxies stand in front of the application.
/// </summary>
public class ReverseProxyConfigurationTests
{
    private static IConfiguration With(string? value) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ReverseProxy.TrustedProxyCountVariable] = value,
            })
            .Build();

    [Fact]
    public void Trusts_nothing_when_unconfigured()
    {
        // The default is the direct deployment: `docker compose up` publishes the port itself,
        // and there a forwarded header is the client's own claim about itself.
        Assert.Equal(ReverseProxy.None, ReverseProxy.TrustedProxyCount(With(null)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("-1")]
    public void Trusts_nothing_for_anything_unusable(string configured)
    {
        // The safe direction for a typo here is the opposite of the rate limit's: an
        // unreadable value must not start trusting a header nobody asked us to trust.
        Assert.Equal(ReverseProxy.None, ReverseProxy.TrustedProxyCount(With(configured)));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("2", 2)]
    [InlineData(" 3 ", 3)]
    public void Honours_a_configured_count(string configured, int expected)
    {
        Assert.Equal(expected, ReverseProxy.TrustedProxyCount(With(configured)));
    }
}
