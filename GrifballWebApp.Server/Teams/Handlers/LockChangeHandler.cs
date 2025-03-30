using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace GrifballWebApp.Server.Teams.Handlers;

public class LockChangeHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext) : SignalRHandler<LockChanged>(hubContext)
{
    public override async Task Handle(LockChanged request, CancellationToken cancellationToken)
    {
        if (request.Value)
        {
            await _hubContext.Clients.AllExcept(request.connectionID!).LockCaptains(request.seasonID);
        }
        else
        {
            await _hubContext.Clients.AllExcept(request.connectionID!).UnlockCaptains(request.seasonID);
        }
    }
}

//public class LockChangeDiscordHandler(RestClient restClient) : DiscordHandler<LockChanged>(restClient)
//{
//    public override async Task Handle(LockChanged request, CancellationToken cancellationToken)
//    {
//        // Send discord message
//    }
//}

public record LockChanged(bool Value, int seasonID, string? connectionID) : INotification;