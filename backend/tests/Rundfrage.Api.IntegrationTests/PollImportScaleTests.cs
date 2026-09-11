using System.Diagnostics;
using System.Net;
using Rundfrage.Api.Data.Entities;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// T020 / SC-001a: a document at the documented maximum imports as one request while the operator
/// waits.
/// </summary>
/// <remarks>
/// This is the criterion that justifies FR-003a's design decision. "One request, no job store" is
/// only defensible if the largest document the limits allow actually finishes inside one - so the
/// largest document the limits allow is what gets imported here. Follows 003's
/// <c>ExportScaleTests</c>, which measured the same thing in the other direction.
/// </remarks>
public class PollImportScaleTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    /// <summary>
    /// Generous on purpose. The point is that the request completes rather than being abandoned;
    /// a tight bound here would make the suite fail on a slow machine for a reason that has
    /// nothing to do with the behaviour under test.
    /// </summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Imports_a_document_at_the_documented_maximum_in_one_request()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        var document = ExportDocumentBuilder.AtDocumentedMaximum();

        var stopwatch = Stopwatch.StartNew();
        var (response, body) = await ImportTestHelper.ImportAsync(factory, document);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.GetProperty("imported").GetBoolean());

        Assert.Equal(Poll.MaxCandidateDays, body.GetProperty("counts").GetProperty("days").GetInt32());
        Assert.Equal(Poll.MaxResponses, body.GetProperty("counts").GetProperty("responses").GetInt32());
        Assert.Equal(
            Poll.MaxCandidateDays * Poll.MaxResponses,
            body.GetProperty("counts").GetProperty("answers").GetInt32());

        Assert.True(
            stopwatch.Elapsed < Budget,
            $"an import at the documented maximum took {stopwatch.Elapsed.TotalSeconds:N1}s, "
            + $"beyond the {Budget.TotalSeconds:N0}s budget SC-001a allows");
    }
}
