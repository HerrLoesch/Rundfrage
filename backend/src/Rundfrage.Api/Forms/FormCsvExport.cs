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
    /// A spreadsheet runs a cell starting with "=", "+", "-" or "@" (and, in some, a tab or a
    /// carriage return) as a formula. Free text comes from anonymous participants, so such a value
    /// is prefixed with an apostrophe and shown as the text it is (OWASP "CSV injection").
    /// </summary>
    public static string NeutraliseFormula(string value) =>
        value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
            ? "'" + value
            : value;

    /// <summary>
    /// The CSV file's raw bytes, UTF-8 with a byte-order mark, valid and header-only when there
    /// are zero responses (FR-039).
    /// </summary>
    /// <remarks>
    /// The byte-order mark is what makes Excel read the file as UTF-8 at all; without it, every
    /// umlaut in a German label or answer arrives garbled. Other readers skip it.
    /// </remarks>
    public static byte[] Build(
        IReadOnlyList<FormField> fieldsInOrder, IReadOnlyList<FormResponseSummary> responses)
    {
        var labeled = FormLabels.Disambiguate(fieldsInOrder);
        var builder = new StringBuilder();

        builder.Append(WriteRow(labeled.Select(l => NeutraliseFormula(l.Label))));

        foreach (var response in responses)
        {
            var values = response.Values.ToDictionary(v => v.FieldId, v => v.Value);

            builder.Append(WriteRow(labeled.Select(l =>
                FormatForCsv(l.Field, values.GetValueOrDefault(l.Field.Id)))));
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(builder.ToString())];
    }

    /// <summary>
    /// A stored canonical value, rendered so a spreadsheet reads it as the correct type
    /// (010 FR-037) - "TRUE"/"FALSE" for Boolean, the stored invariant number text unchanged for
    /// Integer/Decimal, the string itself otherwise. A missing value (010 FR-038) is an empty cell.
    /// </summary>
    /// <remarks>
    /// Only Text and Email can carry a formula: every other type is validated down to digits,
    /// signs and separators, and a leading "-" on a number must stay a minus sign.
    /// </remarks>
    private static string? FormatForCsv(FormField field, string? rawValue)
    {
        if (rawValue is null)
        {
            return null;
        }

        return field.Type switch
        {
            // 010 FR-026: the canonical stored string is "yes"/"no" (see FormFieldValidation.cs).
            FieldType.Boolean => rawValue == "yes" ? "TRUE" : "FALSE",
            FieldType.Text or FieldType.Email => NeutraliseFormula(rawValue),
            _ => rawValue,
        };
    }
}
