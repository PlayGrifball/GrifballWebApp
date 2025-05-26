
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
            try
            {
                await Go(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while executing the DisplayQueueService");
            }
            while (await _timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    await Go(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while executing the DisplayQueueService");
                }
            }
            ;
        }, ct);
        return Task.CompletedTask;
    }

    public async Task Go(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<QueueService>();
        await queueService.Go(ct);
    }
}
