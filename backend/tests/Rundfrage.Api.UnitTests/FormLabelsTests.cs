using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Forms;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// The export-time label disambiguation of 010 FR-035 (research.md R-14 addendum), checked
/// without a database since it is a pure function over already-loaded fields.
/// </summary>
public class FormLabelsTests
{
    private static FormField Field(string label) => new()
    {
        Id = Guid.NewGuid(), FormId = Guid.NewGuid(), Type = FieldType.Text, Label = label,
    };

    [Fact]
    public void Distinct_labels_are_left_unchanged()
    {
        var result = FormLabels.Disambiguate([Field("Name"), Field("E-Mail")]);

        Assert.Equal(["Name", "E-Mail"], result.Select(r => r.Label));
    }

    [Fact]
    public void Duplicate_labels_are_numbered_from_the_second_occurrence()
    {
        var result = FormLabels.Disambiguate([Field("Name"), Field("Name"), Field("Name")]);

        Assert.Equal(["Name", "Name (2)", "Name (3)"], result.Select(r => r.Label));
    }

    [Fact]
    public void A_literal_label_that_looks_like_a_computed_one_does_not_collide_with_it()
    {
        // An operator types "Name (2)" as a literal label for the second field; the third field
        // is a genuine second "Name" and must not be assigned the same key as the second one -
        // that would silently overwrite one field's answer with the other's in every export.
        var result = FormLabels.Disambiguate([Field("Name"), Field("Name (2)"), Field("Name")]);

        var labels = result.Select(r => r.Label).ToArray();
        Assert.Equal(["Name", "Name (2)", "Name (3)"], labels);
        Assert.Equal(labels.Length, labels.Distinct().Count());
    }
}
