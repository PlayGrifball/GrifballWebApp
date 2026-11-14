using GrifballWebApp.Application.Teams;
using GrifballWebApp.Application.Teams.Handlers;

using Microsoft.AspNetCore.SignalR;
using GrifballWebApp.Application.Seasons;

namespace GrifballWebApp.Server.Teams.Handlers;

public class ResortCaptainHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext, SeasonVisibility visibility) : SignalRHandler<Notification<CaptainPlacementDto>>(hubContext, visibility)
{
    public override async Task Handle(Notification<CaptainPlacementDto> request, CancellationToken cancellationToken)
    {
        await (await Others(request.ConnectionId, request.Value.SeasonID)).ResortCaptain(request.Value);
    }
}

