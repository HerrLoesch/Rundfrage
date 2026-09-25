using Rundfrage.Api.Data.Entities;

namespace Rundfrage.Api.Forms;

/// <summary>
/// Disambiguates field labels for export, shared by both formats (010 FR-035, research.md R-14
/// addendum).
/// </summary>
/// <remarks>
/// FR-004 does not require labels to be distinct, but JSON export keys a response's values by
/// label. Two fields named "Name" would collide as one JSON key, so every label after the first
/// occurrence of a duplicate gets " (2)", " (3)", … appended, in field order. A small, local rule
/// with no schema consequence - it is applied only here, at export time.
/// </remarks>
public static class FormLabels
{
    public static IReadOnlyList<(FormField Field, string Label)> Disambiguate(
        IReadOnlyList<FormField> fieldsInOrder)
    {
        // Every candidate is checked against every label already assigned, not only against the
        // count of its own base label - an operator who happens to type "Name (2)" as a literal
        // label would otherwise collide with the computed disambiguation of a second "Name"
        // field, and one of the two would silently overwrite the other's value in every export.
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<(FormField, string)>(fieldsInOrder.Count);

        foreach (var field in fieldsInOrder)
        {
            var candidate = field.Label;
            var suffix = 2;

            while (!assigned.Add(candidate))
            {
                candidate = $"{field.Label} ({suffix})";
                suffix++;
            }

            result.Add((field, candidate));
        }

        return result;
    }
}
