namespace GrifballWebApp.Server.Outbox;

/// <summary>The outbox dispatcher's timings (configuration section <c>Outbox</c>; the defaults suit production).</summary>
public sealed class OutboxOptions
{
    /// <summary>How often the dispatcher looks for due messages when nothing wakes it.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Messages taken at a time.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>
    /// How long a taken message is left to its dispatcher: if it is neither sent nor failed by then (the process
    /// stopped), another try takes it.
    /// </summary>
    public TimeSpan Lease { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>The wait after the first failure, doubled after each one after, up to <see cref="MaxRetryDelay"/>.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Dead-lettered after this many attempts (or its handler's max age, whichever comes first).</summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>Sent and skipped messages are deleted after this long.</summary>
    public TimeSpan SentRetention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Dead letters are deleted after this long (until then the health check reports Degraded).</summary>
    public TimeSpan DeadLetterRetention { get; set; } = TimeSpan.FromDays(30);

    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>The wait before the <paramref name="attempt"/>th attempt's retry.</summary>
    public TimeSpan RetryDelayAfter(int attempt)
    {
        var factor = Math.Pow(2, Math.Clamp(attempt - 1, 0, 30));
        var delay = TimeSpan.FromTicks((long)Math.Min(RetryDelay.Ticks * factor, MaxRetryDelay.Ticks));
        return delay;
    }
}
