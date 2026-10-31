using GrifballWebApp.Server.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace GrifballWebApp.Server.Teams.Handlers;

public class LockChangeHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext) : SignalRHandler<LockChanged>(hubContext)
{
    public override async Task Handle(LockChanged request, CancellationToken cancellationToken)
    {
        if (request.Value)
        {
            await Others(request.connectionID).LockCaptains(request.seasonID);
        }
        else
        {
            await Others(request.connectionID).UnlockCaptains(request.seasonID);
        }
    }
}

public record LockChanged(bool Value, int seasonID, string? connectionID) : INotification;