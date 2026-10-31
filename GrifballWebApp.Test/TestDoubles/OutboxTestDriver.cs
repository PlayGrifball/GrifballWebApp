using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Events;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Outbox;
using GrifballWebApp.Server.Reschedule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace GrifballWebApp.Test;

/// <summary>A clock the test moves.</summary>
public sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}

/// <summary>
/// Runs the outbox's processor, as <see cref="OutboxDispatcher"/> would, on a test's database: its handlers (the
/// matchmaking ones) on <paramref name="discord"/>, every log entry recorded in <see cref="Entries"/>.
/// </summary>
public sealed class OutboxTestDriver : IAsyncDisposable
{
    public record Entry(string Category, LogLevel Level, string Message, Exception? Exception);

    private readonly ServiceProvider _provider;
    private readonly ConcurrentQueue<Entry> _entries = new();
    private readonly string _connectionString;

    public TestTimeProvider Time { get; }

    public OutboxProcessor Processor { get; }

    public OutboxOptions Options { get; }

    public IReadOnlyList<Entry> Entries => _entries.ToArray();

    public OutboxTestDriver(GrifballContext context, IDiscordRestClient discord, DiscordOptions discordOptions, OutboxOptions? options = null, Action<IServiceCollection>? configure = null)
    {
        _connectionString = context.Database.GetConnectionString()!;
        Time = new TestTimeProvider(DateTimeOffset.UtcNow.AddSeconds(1));
        Options = options ?? new OutboxOptions();
        var services = new ServiceCollection()
            .AddLogging(b => b.AddProvider(new Provider(_entries)).SetMinimumLevel(LogLevel.Trace))
            .AddScoped(_ => NewContext())
            .AddScoped<IOutbox, ContextOutbox>()
            .AddSingleton(discord)
            .AddSingleton(Microsoft.Extensions.Options.Options.Create(discordOptions))
            .AddSingleton(Microsoft.Extensions.Options.Options.Create(Options))
            .AddSingleton<TimeProvider>(Time)
            .AddSingleton<OutboxProcessor>()
            .AddMatchmakingOutboxHandlers()
            .AddRescheduleOutboxHandlers()
            .AddEventsOutboxHandlers();
        configure?.Invoke(services);
        _provider = services.BuildServiceProvider();
        Processor = _provider.GetRequiredService<OutboxProcessor>();
    }

    public GrifballContext NewContext() => new(TestDatabase.Options(_connectionString).Options);

    /// <summary>Handles what is due now, until nothing is (the clock stays put: a failed message waits for its retry).</summary>
    public async Task<int> RunDueAsync()
    {
        // Never behind the real clock, which stamps what is queued.
        if (Time.Now < DateTimeOffset.UtcNow)
            Time.Now = DateTimeOffset.UtcNow;
        var total = 0;
        for (var round = 0; round < 100; round++)
        {
            var handled = await Processor.ProcessDueAsync(CancellationToken.None);
            if (handled == 0)
                return total;
            total += handled;
        }
        throw new InvalidOperationException("The outbox never ran dry");
    }

    /// <summary>Handles everything, moving the clock to each next retry or wait, until nothing is left to send.</summary>
    public async Task<int> DrainAsync()
    {
        var total = 0;
        for (var round = 0; round < 100; round++)
        {
            total += await RunDueAsync();
            await using var context = NewContext();
            var toSend = context.Outbox.Where(x => x.Status == OutboxStatus.Pending || x.Status == OutboxStatus.Failed);
            if (!await toSend.AnyAsync())
                return total;
            // Nothing is due now (what is left waits on a retry, or on an earlier message of its group): on to the next retry.
            var now = Time.Now.UtcDateTime;
            var next = await toSend.Where(x => x.NextAttemptAt > now).MinAsync(x => (DateTime?)x.NextAttemptAt);
            if (next is null)
                break;
            Time.Now = new DateTimeOffset(next.Value, TimeSpan.Zero).AddMilliseconds(1);
        }
        var left = string.Join("; ", (await MessagesAsync()).Select(x => $"{x.Id} {x.Kind} {x.Status} attempts {x.Attempts} next {x.NextAttemptAt:O} error {x.LastError}"));
        throw new InvalidOperationException($"The outbox never ran dry by {Time.Now:O}: {left}");
    }

    public async Task<List<OutboxMessage>> MessagesAsync()
    {
        await using var context = NewContext();
        return await context.Outbox.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    private sealed class Provider(ConcurrentQueue<Entry> entries) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Logger(categoryName, entries);

        public void Dispose()
        {
        }
    }

    private sealed class Logger(string category, ConcurrentQueue<Entry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Enqueue(new Entry(category, logLevel, formatter(state, exception), exception));
    }
}
