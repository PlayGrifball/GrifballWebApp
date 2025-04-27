using MediatR;

namespace GrifballWebApp.Server.Matchmaking;

public class UpdateDisplayHandler : INotificationHandler<UpdateDisplayNotification>
{
    private readonly DisplayQueueService _displayQueueService;
    public UpdateDisplayHandler(DisplayQueueService displayQueueService)
    {
        _displayQueueService = displayQueueService;
    }

    public async Task Handle(UpdateDisplayNotification notification, CancellationToken cancellationToken)
    {
        await _displayQueueService.Go(cancellationToken);
    }
}

public class UpdateDisplayNotification : INotification
{

}