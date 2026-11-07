using System.Globalization;
using System.Text.RegularExpressions;
using Rundfrage.Api.Data.Entities;

namespace Rundfrage.Api.Forms;

/// <summary>
/// Whether one submitted value matches its field's type (010 FR-023 to FR-030).
/// </summary>
/// <remarks>
/// This is one half of a rule table written twice - here in C#, and again as
/// <c>useFormFieldValidation.ts</c> on the frontend - kept from silently disagreeing by a shared
/// test fixture rather than a shared runtime (010 research.md R-7). A WASM or generated-parser
/// bridge for seven regex-shaped rules would be the actual unjustified complexity here
/// (Principle III).
/// </remarks>
public static partial class FormFieldValidation
{
    // Anchored with \z rather than $, and with [0-9] rather than \d, so each pattern means what
    // its JavaScript twin means: .NET's $ also matches before a trailing "\n", and its \d also
    // matches non-ASCII digits such as "١٢٣٤٥" - either difference alone lets the server accept
    // a value the browser refuses (FR-030).
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+\z")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^[0-9]{5}\z")]
    private static partial Regex PostalCodePattern();

    [GeneratedRegex(@"^[+-]?[0-9]+\z")]
    private static partial Regex IntegerPattern();

    /// <summary>
    /// Plain positional notation only. <c>double.TryParse</c> alone would also take "NaN",
    /// "Infinity" and "1e5", and the first two cannot be written into the JSON export at all
    /// (FR-037).
    /// </summary>
    [GeneratedRegex(@"^[+-]?([0-9]+\.?[0-9]*|\.[0-9]+)\z")]
    private static partial Regex DecimalPattern();

    /// <summary>
    /// FR-017: whether a submitted value counts as an answer at all. Empty or whitespace-only
    /// text is "not answered" - otherwise a required field could be satisfied by a blank.
    /// </summary>
    public static bool IsAnswered(string? rawValue) => !string.IsNullOrWhiteSpace(rawValue);

    /// <summary>
    /// Whether <paramref name="rawValue"/> is a well-formed value for <paramref name="field"/>.
    /// A <c>null</c> <paramref name="rawValue"/> means the field was left unanswered, which is
    /// valid for every type - requiredness is a separate check (data-model.md §6).
    /// </summary>
    public static bool IsValid(FormField field, string? rawValue)
    {
        if (rawValue is null)
        {
            return true;
        }

        return field.Type switch
        {
            FieldType.Text => IsValidText(field, rawValue),
            FieldType.Integer => IsValidInteger(rawValue),
            FieldType.Decimal => IsValidDecimal(rawValue),
            FieldType.Boolean => rawValue is "yes" or "no",
            FieldType.Email => EmailPattern().IsMatch(rawValue),
            FieldType.Phone => IsValidPhone(rawValue),
            FieldType.PostalCode => PostalCodePattern().IsMatch(rawValue),
            _ => false,
        };
    }

    /// <summary>Digits with an optional sign, within the range the export writes as a number.</summary>
    private static bool IsValidInteger(string value) =>
        IntegerPattern().IsMatch(value)
        && long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _);

    /// <summary>
    /// Positional notation that also stays finite once parsed - a few hundred digits are
    /// well-formed text but overflow a double to infinity.
    /// </summary>
    private static bool IsValidDecimal(string value) =>
        DecimalPattern().IsMatch(value)
        && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
        && double.IsFinite(parsed);

    private static bool IsValidText(FormField field, string value)
    {
        if (field.MinLength is { } min && value.Length < min)
        {
            return false;
        }

        return field.MaxLength is not { } max || value.Length <= max;
    }

    /// <summary>
    /// 010 FR-028: digits with an optional leading "+" and interior spaces, dashes or
    /// parentheses, and at least seven digits in total. A shape check, not a directory lookup.
    /// </summary>
    private static bool IsValidPhone(string value)
    {
        var digitCount = 0;

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (char.IsAsciiDigit(c))
            {
                digitCount++;
                continue;
            }

            if (c == '+' && i == 0)
            {
                continue;
            }

            if (c is ' ' or '-' or '(' or ')')
            {
                continue;
            }

            return false;
        }

        return digitCount >= 7;
    }
}
