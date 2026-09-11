using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Rundfrage.Api.Http;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// T006 / research R-5: the framework's default body-size limits do not apply to an import.
/// </summary>
/// <remarks>
/// Asserted against the request feature rather than by pushing 30 MB through the test host. Two
/// reasons, and the second is the one that decided it: a body that large costs every future run of
/// this suite several seconds, and <c>TestServer</c> does not enforce Kestrel's limit in the first
/// place — so the expensive version of this test would also be the vacuous one, passing whether or
/// not the production code lifted anything.
/// <para>
/// What is checked here is exactly what R-5 identified as the risk: a limit nobody configured,
/// silently contradicting the specification's recorded decision that neither upload is
/// size-limited.
/// </para>
/// </remarks>
public class UploadSizeTests
{
    private sealed class WritableSizeLimit : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;
        public long? MaxRequestBodySize { get; set; } = 30_000_000;
    }

    private sealed class FrozenSizeLimit : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => true;
        public long? MaxRequestBodySize { get; set; } = 30_000_000;
    }

    [Fact]
    public async Task Lifts_the_request_body_size_limit_before_reading_the_upload()
    {
        var context = new DefaultHttpContext();
        var limit = new WritableSizeLimit();
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(limit);

        Assert.Equal(30_000_000, limit.MaxRequestBodySize);

        await ImportUpload.ReceiveAsync(context, CancellationToken.None);

        Assert.Null(limit.MaxRequestBodySize);
    }

    [Fact]
    public async Task Leaves_a_read_only_limit_alone_rather_than_throwing()
    {
        // Some servers freeze the limit once the body has started. Attempting to set it then
        // throws, and an import must not fail for that reason.
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(new FrozenSizeLimit());

        var exception = await Record.ExceptionAsync(
            () => ImportUpload.ReceiveAsync(context, CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task A_request_that_is_not_a_form_yields_nothing_rather_than_throwing()
    {
        var context = new DefaultHttpContext();

        var received = await ImportUpload.ReceiveAsync(context, CancellationToken.None);

        Assert.Null(received);
    }
}
