using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Rundfrage.Api.Observability;

/// <summary>
/// Builds the Serilog logger. Structured entries go to a <see cref="TextWriter"/>, which is
/// <c>Console.Out</c> in production and a buffer under test - so the tested code path and the
/// production code path are the same one (FR-024).
/// </summary>
public static class LoggingSetup
{
    /// <summary>Environment variable carrying the minimum level (FR-025).</summary>
    public const string LogLevelVariable = "LOG_LEVEL";

    private const LogEventLevel DefaultLevel = LogEventLevel.Information;

    /// <summary>
    /// Maps a configured level name to a Serilog level. Anything unrecognised - including
    /// null, blank, and numeric input - falls back to Information rather than throwing, so a
    /// typo in the environment cannot prevent the application from starting.
    /// </summary>
    public static LogEventLevel ResolveMinimumLevel(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return DefaultLevel;
        }

        // Enum.TryParse would also accept numeric strings such as "3"; Enum.IsDefined and the
        // digit guard keep the contract to level *names* only.
        if (char.IsDigit(configured.Trim()[0]))
        {
            return DefaultLevel;
        }

        return Enum.TryParse<LogEventLevel>(configured, ignoreCase: true, out var level)
               && Enum.IsDefined(level)
            ? level
            : DefaultLevel;
    }

    public static Logger CreateLogger(string? logLevel, TextWriter output) =>
        new LoggerConfiguration()
            .MinimumLevel.Is(ResolveMinimumLevel(logLevel))
            // Npgsql logs connection details of its own accord; keeping it at Warning is one of
            // the four measures that keep credentials out of the log (FR-026, research.md R-5).
            .MinimumLevel.Override("Npgsql", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.TextWriter(new CompactJsonFormatter(), output)
            .CreateLogger();
}
