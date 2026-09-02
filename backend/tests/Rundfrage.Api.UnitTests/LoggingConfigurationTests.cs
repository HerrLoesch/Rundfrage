using System.Text.Json;
using Rundfrage.Api.Observability;
using Serilog.Events;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// FR-024: structured entries to stdout via Serilog.
/// FR-025: minimum level configurable through environment configuration.
/// </summary>
public class LoggingConfigurationTests
{
    [Theory]
    [InlineData("Verbose", LogEventLevel.Verbose)]
    [InlineData("Debug", LogEventLevel.Debug)]
    [InlineData("Information", LogEventLevel.Information)]
    [InlineData("Warning", LogEventLevel.Warning)]
    [InlineData("Error", LogEventLevel.Error)]
    [InlineData("fatal", LogEventLevel.Fatal)]
    public void ResolveMinimumLevel_honours_configured_value(string configured, LogEventLevel expected)
    {
        Assert.Equal(expected, LoggingSetup.ResolveMinimumLevel(configured));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-level")]
    public void ResolveMinimumLevel_falls_back_to_Information(string? configured)
    {
        Assert.Equal(LogEventLevel.Information, LoggingSetup.ResolveMinimumLevel(configured));
    }

    [Fact]
    public void Logger_writes_machine_readable_json()
    {
        var output = new StringWriter();
        using var logger = LoggingSetup.CreateLogger("Information", output);

        logger.Information("Probe finished in {DurationMs} ms", 14);

        var line = output.ToString().Trim();
        Assert.False(string.IsNullOrWhiteSpace(line));

        // Must parse as JSON - a plain-text console line would not (FR-024).
        using var parsed = JsonDocument.Parse(line);
        Assert.Equal(14, parsed.RootElement.GetProperty("DurationMs").GetInt32());
    }

    [Fact]
    public void Logger_suppresses_entries_below_the_configured_level()
    {
        var output = new StringWriter();
        using var logger = LoggingSetup.CreateLogger("Warning", output);

        logger.Information("this must not appear");
        logger.Warning("this must appear");

        var text = output.ToString();
        Assert.DoesNotContain("this must not appear", text);
        Assert.Contains("this must appear", text);
    }
}
