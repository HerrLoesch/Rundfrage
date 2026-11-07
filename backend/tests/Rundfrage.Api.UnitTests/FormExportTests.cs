using System.Text.Json;
using System.Text.Json.Serialization;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Forms;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// The JSON export's typing of stored values (010 FR-037), including rows stored before
/// validation refused "NaN", "Infinity" and out-of-range integers.
/// </summary>
public class FormExportTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static (Form Form, FormField Field) FormWith(FieldType type)
    {
        var form = new Form { Id = Guid.NewGuid(), Title = "Test", FormToken = "t", CreatedAt = DateTime.UtcNow };
        var field = new FormField { Id = Guid.NewGuid(), FormId = form.Id, Type = type, Label = "Wert" };
        form.Fields.Add(field);
        return (form, field);
    }

    private static JsonElement ExportedValue(FieldType type, string storedValue)
    {
        var (form, field) = FormWith(type);
        var responses = new[]
        {
            new FormResponseSummary(Guid.NewGuid(), DateTime.UtcNow, [new FormFieldValueView(field.Id, storedValue)]),
        };

        var document = FormExport.Build(form, responses, DateTime.UtcNow);
        var json = JsonSerializer.SerializeToElement(document, Options);

        return json.GetProperty("responses")[0].GetProperty("values").GetProperty("Wert");
    }

    [Theory]
    [InlineData(FieldType.Integer, "42", JsonValueKind.Number)]
    [InlineData(FieldType.Decimal, "3.14", JsonValueKind.Number)]
    [InlineData(FieldType.Boolean, "yes", JsonValueKind.True)]
    [InlineData(FieldType.Boolean, "no", JsonValueKind.False)]
    [InlineData(FieldType.Email, "anna@example.com", JsonValueKind.String)]
    public void Well_formed_values_are_exported_with_their_json_type(
        FieldType type, string stored, JsonValueKind expected)
    {
        Assert.Equal(expected, ExportedValue(type, stored).ValueKind);
    }

    [Theory]
    [InlineData(FieldType.Decimal, "NaN")]
    [InlineData(FieldType.Decimal, "Infinity")]
    [InlineData(FieldType.Integer, "9223372036854775808")]
    public void A_stored_number_the_json_writer_cannot_represent_is_exported_as_its_raw_text(
        FieldType type, string stored)
    {
        // Before the fix this threw while serialising, and one such row failed the whole export.
        var value = ExportedValue(type, stored);

        Assert.Equal(JsonValueKind.String, value.ValueKind);
        Assert.Equal(stored, value.GetString());
    }

    [Fact]
    public void The_document_carries_the_moment_of_export_it_was_given()
    {
        var (form, _) = FormWith(FieldType.Text);
        var exportedAt = new DateTime(2026, 3, 1, 9, 30, 5, DateTimeKind.Utc);

        Assert.Equal(exportedAt, FormExport.Build(form, [], exportedAt).ExportedAt);
    }
}
