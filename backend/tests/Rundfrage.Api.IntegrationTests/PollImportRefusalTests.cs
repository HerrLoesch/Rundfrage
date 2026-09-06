using System.Net.Http.Json;
using System.Net;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Http;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// T016: a refused file creates nothing and names its defect (FR-006, FR-007, FR-013, FR-014,
/// SC-004).
/// </summary>
/// <remarks>
/// Refusal and skipping are different outcomes, and keeping them apart is the feature's core
/// behaviour. Everything here is a refusal: the document cannot yield a poll at all, so nothing
/// is created and one code says why.
/// </remarks>
public class PollImportRefusalTests(SqliteFixture storage) : IClassFixture<SqliteFixture>
{
    private async Task AssertRefusedAsync(ApiFactory factory, MultipartFormDataContent content, string code)
    {
        var before = await PollCountAsync(factory);

        var (response, body) = await ImportTestHelper.ImportAsync(factory, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, ImportTestHelper.Code(body));
        Assert.Equal(before, await PollCountAsync(factory));
    }

    private static async Task<int> PollCountAsync(ApiFactory factory)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var listing = await admin.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/admin/polls");
        return listing.EnumerateArray().Count();
    }

    [Fact]
    public async Task A_file_that_is_not_json_is_refused()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        await AssertRefusedAsync(
            factory,
            ExportDocumentBuilder.FormContentFor("this is not json, it is a sentence"),
            ErrorCodes.NotAValidExport);
    }

    [Fact]
    public async Task Valid_json_that_is_a_different_document_is_refused()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        await AssertRefusedAsync(
            factory,
            ExportDocumentBuilder.FormContentFor("""{"hello":"world"}"""),
            ErrorCodes.NotAValidExport);
    }

    [Fact]
    public async Task A_document_without_a_poll_is_refused()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        await AssertRefusedAsync(
            factory, new ExportDocumentBuilder().WithoutPoll().ToFormContent(), ErrorCodes.NotAValidExport);
    }

    [Fact]
    public async Task A_document_with_no_days_is_refused()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        await AssertRefusedAsync(
            factory, new ExportDocumentBuilder().WithoutDays().ToFormContent(), ErrorCodes.NotAValidExport);
    }

    [Fact]
    public async Task A_newer_format_version_is_refused_rather_than_interpreted()
    {
        // FR-007. A raised version means a field changed meaning (003 FR-020b), so reading it
        // optimistically would import something other than what the file says.
        using var factory = new ApiFactory(storage.DataDirectory);
        await AssertRefusedAsync(
            factory,
            new ExportDocumentBuilder().WithFormatVersion(2).ToFormContent(),
            ErrorCodes.FormatVersionTooNew);
    }

    [Fact]
    public async Task A_title_over_the_limit_is_refused()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        await AssertRefusedAsync(
            factory,
            new ExportDocumentBuilder()
                .WithTitle(ExportDocumentBuilder.OverLimit(Poll.TitleMaxLength)).ToFormContent(),
            ErrorCodes.PollTooLarge);
    }

    [Fact]
    public async Task A_missing_title_is_refused()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        await AssertRefusedAsync(
            factory, new ExportDocumentBuilder().WithTitle(null).ToFormContent(), ErrorCodes.NotAValidExport);
    }

    [Fact]
    public async Task A_message_over_the_limit_is_refused()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        await AssertRefusedAsync(
            factory,
            new ExportDocumentBuilder()
                .WithMessage(ExportDocumentBuilder.OverLimit(Poll.MessageMaxLength)).ToFormContent(),
            ErrorCodes.PollTooLarge);
    }

    [Fact]
    public async Task More_days_than_the_limit_is_refused()
    {
        using var factory = new ApiFactory(storage.DataDirectory);
        await AssertRefusedAsync(
            factory,
            new ExportDocumentBuilder().WithDayCount(Poll.MaxCandidateDays + 1).ToFormContent(),
            ErrorCodes.PollTooLarge);
    }

    [Fact]
    public async Task More_responses_than_the_limit_is_refused_rather_than_truncated()
    {
        // Deliberately a refusal: skipping the 1001st would mean choosing which answers to
        // discard, and no ordering in the file makes that choice defensible (data-model.md §2).
        using var factory = new ApiFactory(storage.DataDirectory);
        await AssertRefusedAsync(
            factory,
            new ExportDocumentBuilder().WithResponses(Poll.MaxResponses + 1).ToFormContent(),
            ErrorCodes.PollTooLarge);
    }

    [Fact]
    public async Task A_title_exactly_at_the_limit_is_accepted()
    {
        // The other side of the boundary, so "too long" cannot quietly become "too long by one".
        using var factory = new ApiFactory(storage.DataDirectory);

        var (response, body) = await ImportTestHelper.ImportAsync(
            factory,
            new ExportDocumentBuilder().WithTitle(ExportDocumentBuilder.AtLimit(Poll.TitleMaxLength)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.GetProperty("imported").GetBoolean());
    }
}
