using Rundfrage.Api.Data.Entities;

namespace Rundfrage.Api.Forms;

/// <summary>
/// A field as both the builder and the participant page receive it (contracts/openapi.yaml,
/// FormFieldDetail). One projection, so the two routes cannot drift apart on what a field is.
/// </summary>
public static class FormFieldView
{
    public static object Of(FormField field) => new
    {
        id = field.Id,
        type = field.Type,
        label = field.Label,
        required = field.Required,
        maxLength = field.MaxLength,
        minLength = field.MinLength,
        displayOrder = field.DisplayOrder,
    };
}
