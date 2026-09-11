using System.Text.Json;
using System.Text.Json.Nodes;
using Rundfrage.Api.Data.Entities;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// Builds export documents for import tests — valid by default, defective on request.
/// </summary>
/// <remarks>
/// Every defect the import must recognise is a named method here rather than a literal document
/// pasted into each test. Two reasons: a test that pastes its own JSON stops resembling what
/// <see cref="Rundfrage.Api.Polls.PollExport"/> actually writes the moment either changes, and a
/// defect spelled out inline reads as an accident rather than as the case under test.
/// <para>
/// The default days are in the future on purpose. Retention runs from the last candidate day, so
/// a document built from past dates is one the import is required to skip as expired - which
/// would make every unrelated test fail for a reason it was not testing.
/// </para>
/// </remarks>
public sealed class ExportDocumentBuilder
{
    private int _formatVersion = 1;
    private string? _title = "Grillabend";
    private string? _message = "Wer kann wann?";
    private List<DateOnly> _days;
    private readonly List<JsonObject> _responses = [];
    private bool _omitPoll;
    private bool _omitDays;

    public ExportDocumentBuilder()
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
        _days = [start, start.AddDays(1), start.AddDays(2)];
    }

    /// <summary>The days the document offers, in the order given.</summary>
    public IReadOnlyList<DateOnly> Days => _days;

    public ExportDocumentBuilder WithFormatVersion(int version)
    {
        _formatVersion = version;
        return this;
    }

    public ExportDocumentBuilder WithTitle(string? title)
    {
        _title = title;
        return this;
    }

    public ExportDocumentBuilder WithMessage(string? message)
    {
        _message = message;
        return this;
    }

    public ExportDocumentBuilder WithDays(params DateOnly[] days)
    {
        _days = [.. days];
        return this;
    }

    /// <summary>Days entirely in the past, so the poll is already past its retention deadline.</summary>
    public ExportDocumentBuilder WithExpiredDays()
    {
        var past = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-120);
        _days = [past, past.AddDays(1)];
        return this;
    }

    public ExportDocumentBuilder WithDayCount(int count)
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
        _days = [.. Enumerable.Range(0, count).Select(i => start.AddDays(i))];
        return this;
    }

    /// <summary>A response answering the given days, all with the same value.</summary>
    public ExportDocumentBuilder WithResponse(
        string displayName, string availability = "yes", IEnumerable<DateOnly>? days = null)
    {
        var answered = days ?? _days;
        return WithRawResponse(displayName, [.. answered.Select(d => (d, availability))]);
    }

    /// <summary>A response whose answers are given exactly, including ones the poll does not offer.</summary>
    public ExportDocumentBuilder WithRawResponse(
        string displayName, IReadOnlyList<(DateOnly Date, string Availability)> answers)
    {
        var array = new JsonArray();
        foreach (var (date, availability) in answers)
        {
            array.Add(new JsonObject
            {
                ["date"] = date.ToString("yyyy-MM-dd"),
                ["availability"] = availability,
            });
        }

        _responses.Add(new JsonObject { ["displayName"] = displayName, ["answers"] = array });
        return this;
    }

    public ExportDocumentBuilder WithResponses(int count, string availability = "yes")
    {
        for (var i = 0; i < count; i++)
        {
            WithResponse($"Person {i}", availability);
        }

        return this;
    }

    /// <summary>Structurally wrong: valid JSON, but not this document.</summary>
    public ExportDocumentBuilder WithoutPoll()
    {
        _omitPoll = true;
        return this;
    }

    public ExportDocumentBuilder WithoutDays()
    {
        _omitDays = true;
        return this;
    }

    /// <summary>The longest value the corresponding limit still allows.</summary>
    public static string AtLimit(int length) => new('a', length);

    /// <summary>One character past it.</summary>
    public static string OverLimit(int length) => new('a', length + 1);

    public JsonObject Build()
    {
        var document = new JsonObject
        {
            ["formatVersion"] = _formatVersion,
            ["exportedAt"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
        };

        if (!_omitPoll)
        {
            var poll = new JsonObject { ["title"] = _title, ["message"] = _message };

            if (!_omitDays)
            {
                var days = new JsonArray();
                foreach (var day in _days)
                {
                    days.Add(new JsonObject { ["date"] = day.ToString("yyyy-MM-dd") });
                }

                poll["days"] = days;
            }

            document["poll"] = poll;
        }

        var responses = new JsonArray();
        foreach (var response in _responses)
        {
            responses.Add(response.DeepClone());
        }

        document["responses"] = responses;

        return document;
    }

    public string ToJson() => Build().ToJsonString(new JsonSerializerOptions { WriteIndented = false });

    /// <summary>The document as multipart content, named as the endpoint expects it.</summary>
    public MultipartFormDataContent ToFormContent(string fileName = "umfrage.json") =>
        FormContentFor(ToJson(), fileName);

    /// <summary>Wraps arbitrary bytes as the upload, for the "this is not an export" cases.</summary>
    public static MultipartFormDataContent FormContentFor(string content, string fileName = "umfrage.json")
    {
        var file = new StringContent(content);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    /// <summary>A builder whose document sits exactly at the documented maximum (SC-001a).</summary>
    public static ExportDocumentBuilder AtDocumentedMaximum() =>
        new ExportDocumentBuilder()
            .WithDayCount(Poll.MaxCandidateDays)
            .WithResponses(Poll.MaxResponses);
}
