using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using GrifballWebApp.Application.Teams.Handlers;

namespace GrifballWebApp.Server.Teams.Handlers;

public class LockChangeHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext, IServiceScopeFactory scopes) : SignalRHandler<LockChanged>(hubContext, scopes)
{
    public override async Task Handle(LockChanged request, CancellationToken cancellationToken)
    {
        if (request.Value)
        {
            await (await Others(request.connectionID, request.seasonID)).LockCaptains(request.seasonID);
        }
        else
        {
            await (await Others(request.connectionID, request.seasonID)).UnlockCaptains(request.seasonID);
        }
    }
}
