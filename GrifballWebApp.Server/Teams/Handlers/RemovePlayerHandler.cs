using GrifballWebApp.Application.Teams;
using GrifballWebApp.Application.Teams.Handlers;

using Microsoft.AspNetCore.SignalR;
using GrifballWebApp.Application.Seasons;

namespace GrifballWebApp.Server.Teams.Handlers;

public class RemovePlayerHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext, SeasonVisibility visibility) : SignalRHandler<Notification<RemovePlayerFromTeamRequestDto>>(hubContext, visibility)
{
    public override async Task Handle(Notification<RemovePlayerFromTeamRequestDto> request, CancellationToken cancellationToken)
    {
        await (await Others(request.ConnectionId, request.Value.SeasonID)).RemovePlayerFromTeam(request.Value);
    }
}
