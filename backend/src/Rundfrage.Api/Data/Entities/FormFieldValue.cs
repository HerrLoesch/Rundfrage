namespace Rundfrage.Api.Data.Entities;

/// <summary>
/// One field's value within one response. A row's absence for a given
/// (<see cref="ResponseId"/>, <see cref="FieldId"/>) pair means "not answered" - the whole
/// mechanism behind a required Boolean field's unanswered state (010 FR-015, research.md R-13)
/// and a missing value on export for a field added after a response existed (010 FR-038,
/// research.md R-14).
/// </summary>
/// <remarks>
/// <see cref="Value"/> holds the canonical, culture-invariant text form of the answer
/// (010 data-model.md §5): the string itself for Text/Email/Phone/PostalCode, invariant-culture
/// number text for Integer/Decimal, and exactly "true" or "false" for Boolean. Type-specific
/// parsing happens only where a typed value is needed - validation and export - never by
/// changing what is stored.
/// </remarks>
public sealed class FormFieldValue
{
    public Guid Id { get; set; }

    public Guid ResponseId { get; set; }

    public FormResponse? Response { get; set; }

    public Guid FieldId { get; set; }

    public FormField? Field { get; set; }

    public required string Value { get; set; }
}
