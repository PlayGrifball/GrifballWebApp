using GrifballWebApp.Application.Teams;
using GrifballWebApp.Application.Teams.Handlers;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Server.Teams.Handlers;

public class MovePlayerHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext, IServiceScopeFactory scopes) : SignalRHandler<Notification<MovePlayerToTeamRequestDto>>(hubContext, scopes)
{
    public override async Task Handle(Notification<MovePlayerToTeamRequestDto> request, CancellationToken cancellationToken)
    {
        await (await Others(request.ConnectionId, request.Value.SeasonID)).MovePlayerToTeam(request.Value);
    }
}
