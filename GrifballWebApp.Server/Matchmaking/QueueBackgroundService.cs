
namespace GrifballWebApp.Server.Matchmaking;

public class QueueBackgroundService : BackgroundService
{
    private readonly ILogger<QueueBackgroundService> _logger;
    private readonly PeriodicTimer _timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
    private Task? _queueTask;
    private readonly IServiceScopeFactory _scopeFactory;
    public QueueBackgroundService(ILogger<QueueBackgroundService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }
    protected override Task ExecuteAsync(CancellationToken ct)
    {
        _queueTask = Task.Run(async () =>
        {
            // Once straight away, then on each tick.
            do
            {
                try
                {
                    await Go(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // Stopping, not an error.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while executing the DisplayQueueService");
                }
            }
            while (await _timer.WaitForNextTickAsync(ct));
        }, ct);
        return Task.CompletedTask;
    }

    // ExecuteAsync hands the loop to Task.Run and returns, so the base StopAsync only cancels it: wait
    // for an iteration still running too, so it doesn't outlive the host's (or a test's) services.
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_queueTask is not null)
            await _queueTask.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    public async Task Go(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<QueueService>();
        await queueService.Go(ct);
    }
}
