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
            // "Request starting HTTP/1.1 GET http://host/api/v1/polls/{the token}" - the one
            // framework message that renders the raw path into its text, where no enricher can
            // reach it. See RequestPathIsACredential below for why that matters here.
            .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.With<RequestPathIsACredential>()
            .WriteTo.TextWriter(new CompactJsonFormatter(), output)
            .CreateLogger();
}

/// <summary>
/// Removes the request path from every log entry, because in this system the request path
/// <i>is</i> a credential.
/// </summary>
/// <remarks>
/// <b>Found by 009's <c>CreatorLoggingTests</c>, and older than 009.</b> ASP.NET Core pushes
/// <c>RequestPath</c> into the logging scope for the duration of a request, and Serilog writes
/// scope properties onto every entry emitted inside it - including this application's own. Under
/// Principle I the token in the URL is the whole authorisation, so every participant answering a
/// poll was writing a working link into the operator's log, and <c>docker compose logs</c> was a
/// list of them.
/// <para>
/// That was already forbidden: 002 FR-043a says a log entry may carry identifiers and counts and
/// never a token. Nothing asserted it until 009 FR-037 made the same demand of the Ersteller link
/// and a test went looking. The fix belongs here rather than in feature 009, because the defect
/// covers <c>/u/</c>, <c>/a/</c>, <c>/w/</c>, <c>/z/</c> and <c>/e/</c> alike.
/// </para>
/// <para>
/// The route <i>template</i> survives - <c>EndpointName</c> still reads
/// <c>HTTP: GET /api/v1/e/{creatorToken}</c> - so an operator can still see which endpoint was
/// reached. Only the value is dropped, which is the part that unlocks something.
/// </para>
/// </remarks>
public sealed class RequestPathIsACredential : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        logEvent.RemovePropertyIfPresent("RequestPath");
        logEvent.RemovePropertyIfPresent("Path");
    }
}
