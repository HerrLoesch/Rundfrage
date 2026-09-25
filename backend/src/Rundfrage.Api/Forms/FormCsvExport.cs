using System.Text;
using Rundfrage.Api.Data.Entities;

namespace Rundfrage.Api.Forms;

/// <summary>
/// A form's responses as CSV: one header per field in builder order, one row per response
/// (010 FR-034, FR-036, FR-037, FR-039; research.md R-5).
/// </summary>
/// <remarks>
/// Hand-written rather than built on a library. Grepping this codebase for existing CSV handling
/// returns nothing - <c>PollExport.cs</c> (feature 003) is JSON-only - and the table this feature
/// exports has no nesting and a small, fixed set of quoting rules, so writing it directly is a few
/// dozen lines. A library's configuration surface (custom converters, streaming, culture-specific
/// formats) would be unjustified against that (Principle III).
/// </remarks>
public static class FormCsvExport
{
    /// <summary>RFC 4180: wrap in double quotes and double any interior quote whenever a value
    /// contains a comma, a quote or a line break.</summary>
    public static string QuoteField(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        var needsQuoting = value.Contains(',') || value.Contains('"')
            || value.Contains('\n') || value.Contains('\r');

        return needsQuoting ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    public static string WriteRow(IEnumerable<string?> fields) =>
        string.Join(',', fields.Select(QuoteField)) + "\r\n";

    /// <summary>
    /// The CSV file's raw bytes, UTF-8, valid and header-only when there are zero responses
    /// (FR-039).
    /// </summary>
    public static byte[] Build(
        IReadOnlyList<FormField> fieldsInOrder, IReadOnlyList<FormResponseSummary> responses)
    {
        var labeled = FormLabels.Disambiguate(fieldsInOrder);
        var builder = new StringBuilder();

        builder.Append(WriteRow(labeled.Select(l => l.Label)));

        foreach (var response in responses)
        {
            var values = response.Values.ToDictionary(v => v.FieldId, v => v.Value);

            builder.Append(WriteRow(labeled.Select(l =>
                FormatForCsv(l.Field, values.GetValueOrDefault(l.Field.Id)))));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    /// <summary>
    /// A stored canonical value, rendered so a spreadsheet reads it as the correct type
    /// (010 FR-037) - "TRUE"/"FALSE" for Boolean, the stored invariant number text unchanged for
    /// Integer/Decimal, the string itself otherwise. A missing value (010 FR-038) is an empty cell.
    /// </summary>
    private static string? FormatForCsv(FormField field, string? rawValue)
    {
        if (rawValue is null)
        {
            return null;
        }

        // 010 FR-026: the canonical stored string is "yes"/"no" (see FormFieldValidation.cs).
        return field.Type == FieldType.Boolean
            ? (rawValue == "yes" ? "TRUE" : "FALSE")
            : rawValue;
    }
}
