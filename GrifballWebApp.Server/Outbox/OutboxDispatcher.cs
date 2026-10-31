using Microsoft.Extensions.Options;

namespace GrifballWebApp.Server.Outbox;

/// <summary>
/// Sends the outbox's messages (<see cref="OutboxProcessor"/>): straight after a commit that queued some
/// (<see cref="OutboxSignal"/>), otherwise every <see cref="OutboxOptions.PollInterval"/> (retries, and messages queued
/// by another process). Deletes old ones every <see cref="OutboxOptions.CleanupInterval"/>.
/// </summary>
public sealed class OutboxDispatcher : BackgroundService
{
    private readonly OutboxProcessor _processor;
    private readonly OutboxSignal _signal;
    private readonly IOptions<OutboxOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<OutboxDispatcher> _logger;
    private Task? _loop;

    public OutboxDispatcher(OutboxProcessor processor, OutboxSignal signal, IOptions<OutboxOptions> options, TimeProvider time, ILogger<OutboxDispatcher> logger)
    {
        _processor = processor;
        _signal = signal;
        _options = options;
        _time = time;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken ct)
    {
        _loop = Task.Run(() => RunAsync(ct), ct);
        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var nextCleanup = _time.GetUtcNow();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Until nothing is due: a group's next message is due once the one before it is sent.
                while (await _processor.ProcessDueAsync(ct) > 0)
                {
                }

                if (_time.GetUtcNow() >= nextCleanup)
                {
                    await _processor.CleanupAsync(ct);
                    nextCleanup = _time.GetUtcNow() + _options.Value.CleanupInterval;
                }

                await _signal.WaitAsync(_options.Value.PollInterval, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The outbox dispatcher failed; trying again in {Delay}", _options.Value.PollInterval);
                try
                {
                    await Task.Delay(_options.Value.PollInterval, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    // ExecuteAsync hands the loop to Task.Run and returns, so the base StopAsync only cancels it: wait for a message
    // still being sent too, so it doesn't outlive the host's services.
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_loop is not null)
            await _loop.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }
}
