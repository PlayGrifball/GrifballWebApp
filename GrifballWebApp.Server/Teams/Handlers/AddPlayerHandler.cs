using GrifballWebApp.Application.Teams;
using GrifballWebApp.Application.Teams.Handlers;

using Microsoft.AspNetCore.SignalR;
using GrifballWebApp.Application.Seasons;

namespace GrifballWebApp.Server.Teams.Handlers;

public class AddPlayerHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext, SeasonVisibility visibility) : SignalRHandler<Notification<AddPlayerToTeamRequestDto>>(hubContext, visibility)
{
    public override async Task Handle(Notification<AddPlayerToTeamRequestDto> request, CancellationToken cancellationToken)
    {
        await (await Others(request.ConnectionId, request.Value.SeasonID)).AddPlayerToTeam(request.Value);
    }
}
