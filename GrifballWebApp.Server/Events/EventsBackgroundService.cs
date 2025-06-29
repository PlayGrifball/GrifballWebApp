
using GrifballWebApp.Server.Events;

namespace GrifballWebApp.Server.Matchmaking;

public class EventsBackgroundService : BackgroundService
{
    private readonly ILogger<EventsBackgroundService> _logger;
    private readonly PeriodicTimer _timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
    private Task? _queueTask;
    private readonly IServiceScopeFactory _scopeFactory;
    public EventsBackgroundService(ILogger<EventsBackgroundService> logger, IServiceScopeFactory scopeFactory)
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
                _logger.LogError(ex, "An error occurred while executing the EventsService");
            }
            while (await _timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    await Go(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while executing the EventsService");
                }
            }
            ;
        }, ct);
        return Task.CompletedTask;
    }

    public async Task Go(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<EventsService>();
        await queueService.Go(ct);
    }
}
