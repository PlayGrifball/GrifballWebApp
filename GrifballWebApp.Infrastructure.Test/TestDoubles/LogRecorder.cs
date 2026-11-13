using Microsoft.Extensions.Logging;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// A logger that keeps every entry (its level and formatted message), for a test that checks what the code logs. Every
/// level is enabled, so a [LoggerMessage] method formats its message as it does in the app.
/// </summary>
internal sealed class LogRecorder<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message)> _entries = [];

    /// <summary>The entries so far, in order.</summary>
    public IReadOnlyList<(LogLevel Level, string Message)> Entries
    {
        get
        {
            lock (_entries)
                return [.. _entries];
        }
    }

    /// <summary>The messages logged at <paramref name="level"/>, in order.</summary>
    public IReadOnlyList<string> At(LogLevel level) => [.. Entries.Where(e => e.Level == level).Select(e => e.Message)];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_entries)
            _entries.Add((logLevel, formatter(state, exception)));
    }
}

/// <summary>A logger factory whose loggers all record into one <see cref="LogRecorder{T}"/>.</summary>
internal sealed class LogRecorderFactory : ILoggerFactory
{
    public LogRecorder<LogRecorderFactory> Logger { get; } = new();

    public ILogger CreateLogger(string categoryName) => Logger;

    public void AddProvider(ILoggerProvider provider)
    {
        // Every logger is the one recording logger: there is nothing to add a provider to
    }

    public void Dispose()
    {
        // Nothing held
    }
}
