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
}
