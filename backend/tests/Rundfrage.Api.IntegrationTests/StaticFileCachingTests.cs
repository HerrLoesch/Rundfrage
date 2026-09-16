using System.Net;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// How the built frontend is cached, which decides whether an open tab survives an update.
/// </summary>
/// <remarks>
/// Every chunk is named after its content and every build replaces them all; index.html is the one
/// file whose name stays put while its content changes. Served without a cache policy, browsers
/// cached it heuristically, and after an update the stale shell asked for chunks that no longer
/// existed - the first area loaded lazily, settings, then failed silently on click. These tests pin
/// the two policies that prevent that: the shell is always revalidated, the hashed assets never are.
/// </remarks>
public sealed class StaticFileCachingTests : IClassFixture<SqliteFixture>, IDisposable
{
    private readonly SqliteFixture _storage;
    private readonly string _webRoot = Path.Combine(
        Path.GetTempPath(), "rundfrage-tests", $"wwwroot-{Guid.NewGuid():n}");

    public StaticFileCachingTests(SqliteFixture storage)
    {
        _storage = storage;

        // The shape Vite produces: the shell at the root, everything it references under assets/
        // with a hash in the name.
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><div id=\"app\"></div>");
        File.WriteAllText(Path.Combine(_webRoot, "assets", "SettingsView-CRmhPUMb.js"), "export {}");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/admin/einstellungen")]
    public async Task The_shell_is_revalidated_on_every_use(string path)
    {
        // Three ways the same file is reached: the default document, its name, and the fallback
        // for a deep link. All of them must carry the same policy, or the tab that entered through
        // the one without it is the tab that breaks after the next deploy.
        using var factory = new ApiFactory(_storage.DataDirectory, webRoot: _webRoot);

        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoCache, "index.html must be served with no-cache");
        Assert.NotNull(response.Headers.ETag);
    }

    [Fact]
    public async Task Hashed_assets_are_cached_for_good()
    {
        // The name changes with the content, so the content behind a given name never changes -
        // which is exactly the case immutable was invented for.
        using var factory = new ApiFactory(_storage.DataDirectory, webRoot: _webRoot);

        var response = await factory.CreateClient().GetAsync("/assets/SettingsView-CRmhPUMb.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=31536000, immutable", response.Headers.CacheControl?.ToString());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/e/abcdefghijklmnopqrstuv")]
    [InlineData("/api/v1/health")]
    public async Task Every_response_asks_the_browser_not_to_leak_the_address(string path)
    {
        // Feature 009 puts a credential in a URL: /e/{token} is the whole authorisation for an
        // Ersteller (009 FR-028d, research R-10). The default referrer policy sends the full URL
        // on a cross-origin navigation, so the token would travel with it.
        //
        // Nothing cross-origin exists today - Principle IV bans external assets - which is why
        // this is defence in depth rather than a fix for a live leak. It is asserted on the
        // participant and API paths as well as the shell, because a header that only some
        // responses carry is the header somebody removes without noticing.
        using var factory = new ApiFactory(_storage.DataDirectory, webRoot: _webRoot);

        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal("same-origin", response.Headers.GetValues("Referrer-Policy").Single());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_webRoot, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
