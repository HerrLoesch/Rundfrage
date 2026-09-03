using Microsoft.AspNetCore.HttpOverrides;

namespace Rundfrage.Api.Http;

/// <summary>
/// Whether, and how far, to believe the <c>X-Forwarded-*</c> headers on an incoming request.
/// </summary>
/// <remarks>
/// Two things the application reads off the connection stop being true the moment a reverse
/// proxy stands in front of it, and both fail silently:
/// <list type="bullet">
/// <item>the request source, which <see cref="RateLimiting"/> partitions by. Behind a proxy every
/// participant arrives from the same address, so FR-027a's ten per hour <em>per source</em>
/// becomes ten per hour for the whole instance - and the eleventh person to answer a poll is
/// refused with a retry hint that will still be wrong in an hour.</item>
/// <item>the request scheme, which decides whether the session cookie carries <c>Secure</c>. The
/// proxy terminates TLS and forwards plain HTTP, so the cookie is issued without the flag on a
/// connection the browser considers secure.</item>
/// </list>
/// <para>
/// Believing the headers is not free: whoever can reach this process directly can then name their
/// own apparent source and answer as often as they like. So it is off until the operator says
/// otherwise, and the direct deployment - <c>docker compose up</c>, which publishes the port
/// itself - keeps the behaviour it had.
/// </para>
/// <para>
/// <see cref="TrustedProxyCountVariable"/> is that statement, and it is a count rather than a
/// flag because the count is what makes the header trustworthy. A proxy <em>appends</em> the
/// address it saw, so with one proxy the rightmost entry is the one the proxy wrote and
/// everything to its left is whatever the caller invented. Reading exactly that many entries from
/// the right is therefore not a formality - it is the whole of the protection.
/// </para>
/// <para>
/// The known-proxy allow-list is deliberately not used: in a container network the proxy's
/// address is assigned by the runtime and changes when it is recreated, so pinning it would be
/// configuration that breaks on a redeploy for reasons nobody connects to this file. The count
/// bounds the trust instead.
/// </para>
/// </remarks>
public static class ReverseProxy
{
    /// <summary>Number of reverse proxies between the browser and this process.</summary>
    public const string TrustedProxyCountVariable = "TRUSTED_PROXY_COUNT";

    /// <summary>No proxy: the headers are the caller's own claims and are ignored.</summary>
    public const int None = 0;

    /// <summary>
    /// The configured count, or <see cref="None"/> for anything unusable.
    /// </summary>
    /// <remarks>
    /// The fallback runs the opposite way to <see cref="RateLimiting.PermitsPerWindow"/>, and for
    /// the same reason: a typo must fall towards trusting less, never towards believing a header
    /// nobody asked us to believe.
    /// </remarks>
    public static int TrustedProxyCount(IConfiguration configuration) =>
        int.TryParse(configuration[TrustedProxyCountVariable], out var configured) && configured > 0
            ? configured
            : None;

    public static void AddTrustedProxyHeaders(
        this IServiceCollection services, IConfiguration configuration)
    {
        var trusted = TrustedProxyCount(configuration);
        if (trusted == None)
        {
            return;
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Only the two that carry a requirement. X-Forwarded-Host is left alone: nothing here
            // builds an absolute URL from it, and accepting it would let a caller choose the host
            // the application believes it is serving.
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = trusted;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
    }

    /// <summary>
    /// Reads the forwarded headers, first in the pipeline so that everything after it - the rate
    /// limiter, the cookie policy - sees the browser's request rather than the proxy's.
    /// </summary>
    public static void UseTrustedProxyHeaders(this WebApplication app, ILogger logger)
    {
        var trusted = TrustedProxyCount(app.Configuration);
        if (trusted == None)
        {
            // Said out loud, because the alternative is a deployment that looks healthy while
            // sharing one submission budget between everyone who answers.
            logger.LogInformation(
                "No reverse proxy configured ({Variable} unset): the request source and scheme "
                + "are taken from the connection itself.", TrustedProxyCountVariable);
            return;
        }

        app.UseForwardedHeaders();
        logger.LogInformation(
            "Trusting the forwarded source and scheme from {TrustedProxyCount} reverse proxy/proxies.",
            trusted);
    }
}
