using Microsoft.AspNetCore.SignalR;

namespace GrifballWebApp.Server.Teams.Handlers;

public class MovePlayerHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext) : SignalRHandler<Notification<MovePlayerToTeamRequestDto>>(hubContext)
{
    public override async Task Handle(Notification<MovePlayerToTeamRequestDto> request, CancellationToken cancellationToken)
    {
        await Others(request.ConnectionId).MovePlayerToTeam(request.Value);
    }
}
