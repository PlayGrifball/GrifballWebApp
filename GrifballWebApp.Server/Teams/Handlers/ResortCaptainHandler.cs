using GrifballWebApp.Application.Teams;
using GrifballWebApp.Application.Teams.Handlers;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Server.Teams.Handlers;

public class ResortCaptainHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext, IServiceScopeFactory scopes) : SignalRHandler<Notification<CaptainPlacementDto>>(hubContext, scopes)
{
    public override async Task Handle(Notification<CaptainPlacementDto> request, CancellationToken cancellationToken)
    {
        await (await Others(request.ConnectionId, request.Value.SeasonID)).ResortCaptain(request.Value);
    }
}

