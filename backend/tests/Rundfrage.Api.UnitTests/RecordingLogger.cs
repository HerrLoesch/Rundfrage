using Microsoft.Extensions.Logging;

namespace Rundfrage.Api.UnitTests;

/// <summary>Captures what a component logs, so 002 FR-026 can be asserted directly.</summary>
public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

public sealed class RecordingLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));

    /// <summary>Everything the logger saw, including exception detail, as one string.</summary>
    public string AllText =>
        string.Join("\n", Entries.Select(e => $"{e.Level} {e.Message} {e.Exception}"));
}
