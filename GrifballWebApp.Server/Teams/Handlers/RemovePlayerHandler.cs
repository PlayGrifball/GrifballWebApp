using Microsoft.AspNetCore.SignalR;

namespace GrifballWebApp.Server.Teams.Handlers;

public class RemovePlayerHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext) : SignalRHandler<Notification<RemovePlayerFromTeamRequestDto>>(hubContext)
{
    public override async Task Handle(Notification<RemovePlayerFromTeamRequestDto> request, CancellationToken cancellationToken)
    {
        await Others(request.ConnectionId).RemovePlayerFromTeam(request.Value);
    }
}
