using GrifballWebApp.Application.Teams;
using GrifballWebApp.Application.Teams.Handlers;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Server.Teams.Handlers;

public class RemovePlayerHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext, IServiceScopeFactory scopes) : SignalRHandler<Notification<RemovePlayerFromTeamRequestDto>>(hubContext, scopes)
{
    public override async Task Handle(Notification<RemovePlayerFromTeamRequestDto> request, CancellationToken cancellationToken)
    {
        await (await Others(request.ConnectionId, request.Value.SeasonID)).RemovePlayerFromTeam(request.Value);
    }
}
