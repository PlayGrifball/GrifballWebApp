using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace GrifballWebApp.Test;

/// <summary>
/// Minimal ILogger that records every log entry so tests can assert on level / message / exception.
/// </summary>
public class RecordingLogger<T> : ILogger<T>
{
    public record Entry(LogLevel Level, string Message, Exception? Exception);

    private readonly ConcurrentQueue<Entry> _entries = new();
    private readonly SemaphoreSlim _signal = new(0);

    public IReadOnlyList<Entry> Entries => _entries.ToArray();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _entries.Enqueue(new Entry(logLevel, formatter(state, exception), exception));
        _signal.Release();
    }

    /// <summary>Waits until at least <paramref name="count"/> entries have been logged, or the timeout elapses.</summary>
    public async Task<bool> WaitForEntries(int count, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (_entries.Count < count)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                return false;
            await _signal.WaitAsync(remaining);
        }
        return true;
    }
}
