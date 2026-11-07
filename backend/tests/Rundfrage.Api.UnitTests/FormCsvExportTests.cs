using System.Text;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Forms;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// The RFC 4180 quoting rules of the hand-written CSV writer (010 FR-034, FR-036, FR-037;
/// research.md R-5) - no library, since the shape needed is one flat table with a small, fixed
/// set of quoting rules.
/// </summary>
public class FormCsvExportTests
{
    [Fact]
    public void A_plain_value_is_written_unquoted()
    {
        Assert.Equal("Anna", FormCsvExport.QuoteField("Anna"));
    }

    [Fact]
    public void A_value_containing_a_comma_is_quoted()
    {
        Assert.Equal("\"Anna, Beatrix\"", FormCsvExport.QuoteField("Anna, Beatrix"));
    }

    [Fact]
    public void A_value_containing_a_quote_is_quoted_and_the_quote_is_doubled()
    {
        Assert.Equal("\"Sie sagte \"\"Hallo\"\"\"", FormCsvExport.QuoteField("Sie sagte \"Hallo\""));
    }

    [Fact]
    public void A_value_containing_a_line_break_is_quoted()
    {
        Assert.Equal("\"Zeile 1\nZeile 2\"", FormCsvExport.QuoteField("Zeile 1\nZeile 2"));
    }

    [Fact]
    public void An_empty_or_null_value_is_written_as_an_empty_field()
    {
        Assert.Equal("", FormCsvExport.QuoteField(null));
        Assert.Equal("", FormCsvExport.QuoteField(""));
    }

    [Fact]
    public void A_row_joins_its_fields_with_commas_and_ends_with_crlf()
    {
        var row = FormCsvExport.WriteRow(["Anna", "30", "anna@example.com"]);
        Assert.Equal("Anna,30,anna@example.com\r\n", row);
    }

    [Fact]
    public void A_row_with_a_field_needing_quoting_still_joins_correctly()
    {
        var row = FormCsvExport.WriteRow(["Anna, Beatrix", "30"]);
        Assert.Equal("\"Anna, Beatrix\",30\r\n", row);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://evil\")")]
    [InlineData("+1+1")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1)")]
    [InlineData("\tTab")]
    public void A_value_a_spreadsheet_would_run_as_a_formula_is_prefixed_with_an_apostrophe(string value)
    {
        Assert.Equal("'" + value, FormCsvExport.NeutraliseFormula(value));
    }

    [Theory]
    [InlineData("Anna")]
    [InlineData("anna@example.com")]
    [InlineData("")]
    public void An_ordinary_value_is_left_as_it_is(string value)
    {
        Assert.Equal(value, FormCsvExport.NeutraliseFormula(value));
    }

    [Fact]
    public void Free_text_and_email_answers_are_neutralised_but_a_negative_number_stays_a_number()
    {
        var text = new FormField { Id = Guid.NewGuid(), Type = FieldType.Text, Label = "Kommentar", MaxLength = 100 };
        var email = new FormField { Id = Guid.NewGuid(), Type = FieldType.Email, Label = "E-Mail" };
        var number = new FormField { Id = Guid.NewGuid(), Type = FieldType.Integer, Label = "Saldo" };
        var response = new FormResponseSummary(Guid.NewGuid(), DateTime.UtcNow,
        [
            new FormFieldValueView(text.Id, "=1+1"),
            new FormFieldValueView(email.Id, "=x@example.com"),
            new FormFieldValueView(number.Id, "-5"),
        ]);

        var bytes = FormCsvExport.Build([text, email, number], [response]);
        var rows = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n");

        Assert.Equal("'=1+1,'=x@example.com,-5", rows[1]);
    }

    [Fact]
    public void A_label_starting_like_a_formula_is_neutralised_in_the_header_too()
    {
        var field = new FormField { Id = Guid.NewGuid(), Type = FieldType.Text, Label = "=Summe", MaxLength = 10 };

        var bytes = FormCsvExport.Build([field], []);

        Assert.Equal("'=Summe\r\n", Encoding.UTF8.GetString(bytes[3..]));
    }
}
