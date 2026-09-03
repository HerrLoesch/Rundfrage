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
            // The data-access stack logs of its own accord, and what it logs is the storage
            // location. Keeping it at Warning is one of the measures that keep that out of the
            // log (002 FR-026, research.md R-5). The Npgsql override that used to sit here went
            // with the database driver.
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Data.Sqlite", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.TextWriter(new CompactJsonFormatter(), output)
            .CreateLogger();
}
