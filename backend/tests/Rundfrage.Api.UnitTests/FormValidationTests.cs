using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Forms;
using Rundfrage.Api.Http;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// The structural rules of 010 FR-001 to FR-011a, checked without a database so that "enforced on
/// the server" has exactly one place where it is true (mirrors WishListValidationTests).
/// </summary>
public class FormValidationTests
{
    private static FormFieldDraft TextField(
        string label = "Name", int? maxLength = 100, int? minLength = null, bool required = false) =>
        new(FieldType.Text, label, required, maxLength, minLength);

    [Fact]
    public void A_title_is_required()
    {
        Assert.Equal(ErrorCodes.TitleRequired, FormService.ValidateTitle(null)?.Code);
        Assert.Equal(ErrorCodes.TitleRequired, FormService.ValidateTitle("   ")?.Code);
    }

    [Fact]
    public void A_title_has_a_limit_and_the_refusal_names_it()
    {
        var error = FormService.ValidateTitle(new string('x', Form.TitleMaxLength + 1));

        Assert.Equal(ErrorCodes.TitleTooLong, error?.Code);
        Assert.Equal(Form.TitleMaxLength, error?.Limit);
    }

    [Fact]
    public void A_field_needs_a_type()
    {
        var draft = new FormFieldDraft(null, "Name", false, null, null);
        Assert.Equal(ErrorCodes.FieldTypeInvalid, FormService.ValidateNewField(draft)?.Code);
    }

    [Fact]
    public void A_field_needs_a_label()
    {
        Assert.Equal(ErrorCodes.LabelRequired, FormService.ValidateNewField(TextField(label: null!))?.Code);
        Assert.Equal(ErrorCodes.LabelRequired, FormService.ValidateNewField(TextField(label: "  "))?.Code);
    }

    [Fact]
    public void A_label_has_a_limit_and_the_refusal_names_it()
    {
        var error = FormService.ValidateNewField(
            TextField(label: new string('x', FormField.LabelMaxLength + 1)));

        Assert.Equal(ErrorCodes.LabelTooLong, error?.Code);
        Assert.Equal(FormField.LabelMaxLength, error?.Limit);
    }

    [Fact]
    public void A_new_text_field_requires_a_maximum_length()
    {
        var draft = new FormFieldDraft(FieldType.Text, "Name", false, null, null);
        Assert.Equal(ErrorCodes.TextMaxLengthMissing, FormService.ValidateNewField(draft)?.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(FormField.TextLengthCeiling + 1)]
    public void A_text_maximum_length_outside_the_bound_is_refused(int maxLength)
    {
        var error = FormService.ValidateNewField(TextField(maxLength: maxLength));
        Assert.Equal(ErrorCodes.TextLengthRangeInvalid, error?.Code);
    }

    [Fact]
    public void A_text_minimum_may_not_exceed_its_maximum()
    {
        var error = FormService.ValidateNewField(TextField(maxLength: 10, minLength: 11));
        Assert.Equal(ErrorCodes.TextLengthRangeInvalid, error?.Code);
    }

    [Fact]
    public void A_text_field_with_sane_limits_is_accepted()
    {
        Assert.Null(FormService.ValidateNewField(TextField(maxLength: 100, minLength: 1)));
        Assert.Null(FormService.ValidateNewField(TextField(maxLength: 100, minLength: null)));
    }

    [Theory]
    [InlineData(FieldType.Integer)]
    [InlineData(FieldType.Decimal)]
    [InlineData(FieldType.Boolean)]
    [InlineData(FieldType.Email)]
    [InlineData(FieldType.Phone)]
    [InlineData(FieldType.PostalCode)]
    public void A_non_text_field_needs_no_length_limits(FieldType type)
    {
        var draft = new FormFieldDraft(type, "Frage", false, null, null);
        Assert.Null(FormService.ValidateNewField(draft));
    }

    [Fact]
    public void Editing_a_field_never_validates_a_type_because_there_is_no_parameter_for_one()
    {
        // 010 FR-008, spec clarification 2026-09-22: there is no ValidateFieldEdit overload that
        // accepts a new type, which is what makes "type is immutable" true by the absence of a
        // code path rather than by a check that could be bypassed.
        Assert.Null(FormService.ValidateFieldEdit(FieldType.Text, "Neuer Name", 100, null));
    }

    [Fact]
    public void Editing_a_text_fields_limits_is_validated_the_same_way_as_creating_one()
    {
        var error = FormService.ValidateFieldEdit(FieldType.Text, null, 10, 11);
        Assert.Equal(ErrorCodes.TextLengthRangeInvalid, error?.Code);
    }
}
