using GrifballWebApp.Application.Teams;
using GrifballWebApp.Application.Teams.Handlers;

using Microsoft.AspNetCore.SignalR;
using GrifballWebApp.Application.Seasons;

namespace GrifballWebApp.Server.Teams.Handlers;

public class MovePlayerHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext, SeasonVisibility visibility) : SignalRHandler<Notification<MovePlayerToTeamRequestDto>>(hubContext, visibility)
{
    public override async Task Handle(Notification<MovePlayerToTeamRequestDto> request, CancellationToken cancellationToken)
    {
        await (await Others(request.ConnectionId, request.Value.SeasonID)).MovePlayerToTeam(request.Value);
    }
}
