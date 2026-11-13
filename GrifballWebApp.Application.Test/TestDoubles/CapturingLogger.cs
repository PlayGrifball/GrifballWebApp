using Microsoft.Extensions.Logging;

namespace GrifballWebApp.Application.Test.TestDoubles;

/// <summary>
/// An ILogger that keeps what is written to it, every level enabled: for a service whose log line is what it reports (an
/// import's or a refresh's summary for the operator). A substituted ILogger says no level is enabled, so a
/// [LoggerMessage] method writes nothing to it.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message)> _entries = [];
    private readonly Lock _lock = new();

    /// <summary>Each entry written, its level and its formatted message, in order.</summary>
    public IReadOnlyList<(LogLevel Level, string Message)> Entries
    {
        get
        {
            lock (_lock)
                return _entries.ToList();
        }
    }

    /// <summary>The formatted messages written, in order.</summary>
    public IReadOnlyList<string> Messages => Entries.Select(e => e.Message).ToList();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_lock)
            _entries.Add((logLevel, formatter(state, exception)));
    }
}
