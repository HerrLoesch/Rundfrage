namespace Rundfrage.Api.Data.Entities;

/// <summary>
/// One question on a <see cref="Form"/> (010 FR-003 to FR-006). Deleting it deletes every
/// <see cref="FormFieldValue"/> collected for it, across every response (010 FR-009).
/// </summary>
/// <remarks>
/// <see cref="Type"/> is fixed for this field's whole lifetime - no request may change it
/// (010 FR-008, spec clarification 2026-09-22). Reinterpreting values already collected under a
/// different type has no defined meaning, so the simplest correct answer is that the question
/// never arises: there is no setter path for it beyond construction.
/// </remarks>
public sealed class FormField
{
    public const int LabelMaxLength = 200;

    /// <summary>
    /// A system ceiling on a Text field's <see cref="MaxLength"/> - not a number FR-006 states,
    /// but a bound so a single field cannot become an unbounded blob while comfortably exceeding
    /// any legitimate open-text answer (010 data-model.md §2).
    /// </summary>
    public const int TextLengthCeiling = 5000;

    public Guid Id { get; set; }

    public Guid FormId { get; set; }

    public Form? Form { get; set; }

    public FieldType Type { get; set; }

    public required string Label { get; set; }

    public bool Required { get; set; }

    /// <summary>Required (non-null) when <see cref="Type"/> is Text; otherwise always null (FR-006).</summary>
    public int? MaxLength { get; set; }

    /// <summary>Text only. Null means no minimum.</summary>
    public int? MinLength { get; set; }

    /// <summary>
    /// Dense, 0..N-1 within one form, renumbered on every add/remove/reorder (010 research.md R-4).
    /// </summary>
    public int DisplayOrder { get; set; }

    public List<FormFieldValue> Values { get; set; } = [];
}
