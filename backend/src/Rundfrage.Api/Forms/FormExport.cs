using System.Globalization;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Http;
using Rundfrage.Api.Time;

namespace Rundfrage.Api.Forms;

/// <summary>Matches FormExportDocument and its schemas in contracts/openapi.yaml.</summary>
public sealed record FormExportDocument(
    int FormatVersion, DateTime ExportedAt, ExportedForm Form, IReadOnlyList<ExportedFormResponse> Responses);

public sealed record ExportedFormField(string Label, FieldType Type);

public sealed record ExportedForm(string Title, IReadOnlyList<ExportedFormField> Fields);

public sealed record ExportedFormResponse(
    DateTime SubmittedAt, IReadOnlyDictionary<string, object?> Values);

/// <summary>
/// A form's responses as JSON, one object per response, values keyed by (disambiguated) field
/// label and typed (010 FR-035, FR-036, FR-037, FR-039). Mirrors <c>Polls/PollExport.cs</c>'s
/// versioned-envelope shape.
/// </summary>
/// <remarks>
/// Builds from data already fetched by <see cref="FormService"/> - <c>FindAsync</c> for the
/// ordered fields, <c>ListResponsesAsync</c> for the responses - rather than a second querying
/// path over the same tables (Principle III).
/// </remarks>
public sealed class FormExport(BerlinClock clock)
{
    /// <summary>Additive changes keep this number, matching PollExport's own convention.</summary>
    public const int FormatVersion = 1;

    public FormExportDocument Build(
        Form form, IReadOnlyList<FormResponseSummary> responses)
    {
        var fieldsInOrder = form.Fields.OrderBy(f => f.DisplayOrder).ToList();
        var labeled = FormLabels.Disambiguate(fieldsInOrder);

        var exportedForm = new ExportedForm(
            form.Title,
            labeled.Select(l => new ExportedFormField(l.Label, l.Field.Type)).ToList());

        var exportedResponses = responses.Select(response =>
        {
            var raw = response.Values.ToDictionary(v => v.FieldId, v => v.Value);

            var values = new Dictionary<string, object?>();
            foreach (var (field, label) in labeled)
            {
                // FR-038: a response with no value for a field is omitted, never a guessed default.
                if (raw.TryGetValue(field.Id, out var rawValue))
                {
                    values[label] = TypedValue(field, rawValue);
                }
            }

            return new ExportedFormResponse(response.SubmittedAt, values);
        }).ToList();

        return new FormExportDocument(FormatVersion, clock.Now, exportedForm, exportedResponses);
    }

    private static object? TypedValue(FormField field, string rawValue) => field.Type switch
    {
        FieldType.Integer => long.Parse(rawValue, CultureInfo.InvariantCulture),
        FieldType.Decimal => double.Parse(rawValue, CultureInfo.InvariantCulture),
        // 010 FR-026: the canonical stored string is "yes"/"no" (research the boolean fix note in
        // FormFieldValidation.cs); the JSON export still renders a real boolean per FR-037.
        FieldType.Boolean => rawValue == "yes",
        _ => rawValue,
    };

    /// <summary>Names the file so several exports can share a folder without overwriting each other.</summary>
    public static string FileNameFor(string title, DateTime takenAtUtc, string extension) =>
        DownloadFileName.WithTimestamp(
            DownloadFileName.Slugify(title, fallback: "formular"), takenAtUtc, extension);
}
