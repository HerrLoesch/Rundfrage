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
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex PostalCodePattern();

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
            FieldType.Integer => long.TryParse(rawValue, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _),
            FieldType.Decimal => double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
            FieldType.Boolean => rawValue is "yes" or "no",
            FieldType.Email => EmailPattern().IsMatch(rawValue),
            FieldType.Phone => IsValidPhone(rawValue),
            FieldType.PostalCode => PostalCodePattern().IsMatch(rawValue),
            _ => false,
        };
    }

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
