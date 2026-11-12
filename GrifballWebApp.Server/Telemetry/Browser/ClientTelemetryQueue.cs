using System.Threading.Channels;

namespace GrifballWebApp.Server.Telemetry.Browser;

/// <summary>
/// The browser's batches waiting for the collector: bounded, so a collector that is slow or away costs memory up to
/// <see cref="Capacity"/> batches, and drops the newest past that rather than hold requests open.
/// </summary>
public sealed partial class ClientTelemetryQueue(ILogger<ClientTelemetryQueue> logger, ForwardFailures failures)
{
    public const int Capacity = 1_000;

    private readonly Channel<(ClientTelemetryBatchDto Batch, string UserAgent)> _channel =
        // Wait, not a Drop mode: those report every write as done, so a full queue couldn't be told (and warned of)
        Channel.CreateBounded<(ClientTelemetryBatchDto, string)>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

    /// <summary>Queues a batch with spans or logs to forward; one queue full drops it (warned of at most once a minute).</summary>
    public void Enqueue(ClientTelemetryBatchDto batch, string userAgent)
    {
        if (batch.Spans.Count == 0 && batch.Logs.Count == 0)
            return;
        if (!_channel.Writer.TryWrite((batch, userAgent)) && failures.ShouldLog(out var suppressed))
            LogFull(logger, Capacity, suppressed);
    }

    /// <summary>Forwards every queued batch as it comes, until <paramref name="ct"/> (the host stopping).</summary>
    public async Task ForwardAsync(Func<ClientTelemetryForwarder> forwarder, CancellationToken ct)
    {
        await foreach (var (batch, userAgent) in _channel.Reader.ReadAllAsync(ct))
            await forwarder().Forward(batch, userAgent, ct);
    }

    /// <summary>Forwards what is queued now, and returns: for tests.</summary>
    public async Task ForwardQueuedAsync(ClientTelemetryForwarder forwarder, CancellationToken ct = default)
    {
        while (_channel.Reader.TryRead(out var item))
            await forwarder.Forward(item.Batch, item.UserAgent, ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The browser's telemetry queue is full ({Capacity} batches): dropping batches; {Suppressed} more failures since the last warning")]
    private static partial void LogFull(ILogger logger, int capacity, int suppressed);
}

/// <summary>Forwards the queued batches to the collector, in the background, for the host's life.</summary>
public sealed class ClientTelemetryPump(ClientTelemetryQueue queue, IServiceProvider services) : BackgroundService
{
    // A forwarder for each batch: it's a typed HttpClient, which IHttpClientFactory hands out per use
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        queue.ForwardAsync(() => services.GetRequiredService<ClientTelemetryForwarder>(), stoppingToken);
}
